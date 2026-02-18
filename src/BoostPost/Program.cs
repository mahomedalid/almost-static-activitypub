using System.Text.Json;
using System.CommandLine;
using System.Text.Json.Serialization;
using Azure.Data.Tables;
using ActivityPubDotNet.Core;
using ActivityPubDotNet.Core.Storage;
using Azure;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BoostPost;

var serviceCollection = new ServiceCollection();

ConfigureServices(serviceCollection, args);

using var serviceProvider = serviceCollection.BuildServiceProvider();

var loggerFactory = serviceProvider.GetService<ILoggerFactory>()!;

var accountOption = new Option<string>("--account", "The ActivityPub actor URI to boost posts from")
{
    IsRequired = true
};

var actorOption = new Option<string>("--actor", "Your own ActivityPub actor URI")
{
    IsRequired = true
};

var boostedFileOption = new Option<string>(
    "--boostedFile",
    () => "boosted.txt",
    "File to track already-boosted note URIs");

var maxOption = new Option<int>(
    "--max",
    () => 1,
    "Maximum number of notes to boost per run");

var privateKey = Environment.GetEnvironmentVariable("ACTIVITYPUB_DOTNET_PRIVATEKEY")!;
var keyId = Environment.GetEnvironmentVariable("ACTIVITYPUB_DOTNET_KEYID")!;
var connectionString = Environment.GetEnvironmentVariable("ACTIVITYPUB_DOTNET_STORAGE_CONNECTIONSTRING")!;

var rootCommand = new RootCommand("Boost (Announce) original notes from an ActivityPub account to your followers");

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = new ContextNamePolicy(),
    WriteIndented = true
};

rootCommand.SetHandler(async (string account, string actor, string boostedFile, int max) =>
{
    var logger = loggerFactory.CreateLogger<Program>();

    var actorHelper = new ActorHelper(privateKey, keyId);
    actorHelper.Logger = logger;

    // Load already-boosted notes from tracking file
    var boostedNotes = new HashSet<string>();
    if (File.Exists(boostedFile))
    {
        boostedNotes = new HashSet<string>(
            File.ReadAllLines(boostedFile).Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    // 1. Fetch target account actor to get outbox URL
    logger.LogInformation($"Fetching actor information for {account}");
    var accountActor = await actorHelper.FetchActorInformationAsync(account);
    var outboxUrl = accountActor.Outbox;
    logger.LogInformation($"Outbox URL: {outboxUrl}");

    // 2. Fetch the outbox collection
    var outboxJson = await actorHelper.SendGetSignedRequest(new Uri(outboxUrl));
    var outbox = JsonNode.Parse(outboxJson)!.AsObject();

    // 3. Navigate to the first page of the outbox
    JsonObject page;
    var firstNode = outbox["first"];

    if (firstNode is JsonObject firstObj)
    {
        // Some servers embed the first page inline
        page = firstObj;
    }
    else if (firstNode != null)
    {
        var firstPageUrl = firstNode.ToString();
        var pageJson = await actorHelper.SendGetSignedRequest(new Uri(firstPageUrl));
        page = JsonNode.Parse(pageJson)!.AsObject();
    }
    else if (outbox.ContainsKey("orderedItems"))
    {
        // Items are directly in the outbox collection
        page = outbox;
    }
    else
    {
        logger.LogWarning("Could not find outbox items");
        return;
    }

    var items = page["orderedItems"]?.AsArray();
    if (items == null || items.Count == 0)
    {
        logger.LogWarning("No items found in outbox");
        return;
    }

    // 4. Filter for Create activities whose object is a Note (skip Announce/boost items)
    var notesToBoost = new List<string>();
    foreach (var item in items)
    {
        var type = item?["type"]?.ToString();

        // Only process Create activities — skip Announce (boosts by the account)
        if (type != "Create")
            continue;

        var obj = item?["object"];
        if (obj is JsonObject objNode)
        {
            var objType = objNode["type"]?.ToString();
            if (objType == "Note")
            {
                var noteId = objNode["id"]?.ToString();
                if (noteId != null && !boostedNotes.Contains(noteId))
                {
                    notesToBoost.Add(noteId);
                }
            }
        }
        // If object is a plain URI string we cannot determine its type, so skip it
    }

    logger.LogInformation($"Found {notesToBoost.Count} new note(s) eligible to boost (max: {max})");

    if (notesToBoost.Count == 0)
    {
        logger.LogInformation("Nothing new to boost");
        return;
    }

    // 5. Collect follower endpoints from Azure Table Storage
    var tableClient = new TableServiceClient(connectionString);
    var followersTable = tableClient.GetTableClient("followers");
    Pageable<Follower> queryResultsFilter = followersTable.Query<Follower>();
    var followers = queryResultsFilter.ToList();

    logger.LogInformation($"Loaded {followers.Count} follower(s)");

    // 6. Send Announce for each note
    var boostedCount = 0;
    foreach (var noteUri in notesToBoost)
    {
        if (boostedCount >= max) break;

        var published = DateTime.UtcNow.ToString("o");
        var announceId = $"{actor}#boosts/{Guid.NewGuid()}";

        var announce = new AnnounceActivity
        {
            Id = announceId,
            Actor = actor,
            Published = published,
            Object = noteUri,
            Cc = new List<string> { $"{actor}/followers", account }
        };

        var announceJson = JsonSerializer.Serialize(announce, options);
        logger.LogInformation($"Boosting note: {noteUri}");
        logger.LogDebug($"Announce JSON: {announceJson}");

        var endpointsAlreadySent = new List<string>();

        foreach (var follower in followers)
        {
            // Skip followers that previously errored
            if (follower.LastError != null)
            {
                logger.LogInformation($"Skipping errored follower {follower.ActorUri}: {follower.LastError} ({follower.LastErrorDate})");
                continue;
            }

            logger.LogInformation($"Fetching follower actor information: {follower.ActorUri}");
            string endpointUri = string.Empty;

            try
            {
                var followerActor = await actorHelper.FetchActorInformationAsync(follower.ActorUri);
                endpointUri = followerActor.Endpoints?.SharedInbox ?? followerActor.Inbox;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());

                // Mark follower as errored in the table
                follower.LastError = ex.Message;
                follower.LastErrorDate = DateTimeOffset.UtcNow;
                await followersTable.UpdateEntityAsync(follower, follower.ETag, TableUpdateMode.Merge);
                continue;
            }

            logger.LogInformation($"Inbox: {endpointUri}");

            try
            {
                if (endpointsAlreadySent.Contains(endpointUri))
                {
                    logger.LogInformation($"Skipping {endpointUri}");
                    continue;
                }

                endpointsAlreadySent.Add(endpointUri);

                await actorHelper.SendPostSignedRequest(announceJson, new Uri(endpointUri));

                // Mark follower as successful
                follower.LastSuccessDate = DateTimeOffset.UtcNow;
                follower.LastError = null;
                follower.LastErrorDate = null;
                await followersTable.UpdateEntityAsync(follower, follower.ETag, TableUpdateMode.Merge);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());

                // Mark follower as errored in the table
                follower.LastError = ex.Message;
                follower.LastErrorDate = DateTimeOffset.UtcNow;
                await followersTable.UpdateEntityAsync(follower, follower.ETag, TableUpdateMode.Merge);
            }
        }

        // Also deliver to the original account's inbox so they see the boost
        try
        {
            var accountInbox = accountActor.Endpoints?.SharedInbox ?? accountActor.Inbox;
            if (!endpointsAlreadySent.Contains(accountInbox))
            {
                logger.LogInformation($"Sending boost notification to original account: {accountInbox}");
                await actorHelper.SendPostSignedRequest(announceJson, new Uri(accountInbox));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
        }

        // Record the note as boosted
        File.AppendAllText(boostedFile, noteUri + Environment.NewLine);
        boostedCount++;
        logger.LogInformation($"Successfully boosted: {noteUri}");
    }

    logger.LogInformation($"Boosted {boostedCount} note(s) total");

}, accountOption, actorOption, boostedFileOption, maxOption);

rootCommand.AddOption(accountOption);
rootCommand.AddOption(actorOption);
rootCommand.AddOption(boostedFileOption);
rootCommand.AddOption(maxOption);

var result = await rootCommand.InvokeAsync(args);

return result;

static void ConfigureServices(ServiceCollection serviceCollection, string[] args)
{
    serviceCollection
        .AddLogging(configure =>
        {
            configure.AddSimpleConsole(options => options.TimestampFormat = "hh:mm:ss ");

            if (args.Any("--debug".Contains))
            {
                configure.SetMinimumLevel(LogLevel.Debug);
            }
        });
}

internal class ContextNamePolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (name.Equals("_context"))
        {
            return "@context";
        }

        return Char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
