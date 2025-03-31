using System.Text.Json;
using System.CommandLine.Parsing;
using System.CommandLine;
using System.Text.Json.Serialization;
using Azure.Data.Tables;
using ActivityPubDotNet;
using ActivityPubDotNet.Core;
using Azure;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var serviceCollection = new ServiceCollection();

ConfigureServices(serviceCollection, args);

using var serviceProvider = serviceCollection.BuildServiceProvider();

var loggerFactory = serviceProvider.GetService<ILoggerFactory>()!;

var notePathOption = new Option<string>("--notePath")
{
    IsRequired = true
};

var privateKey = Environment.GetEnvironmentVariable("ACTIVITYPUB_DOTNET_PRIVATEKEY")!;
var keyId = Environment.GetEnvironmentVariable("ACTIVITYPUB_DOTNET_KEYID")!;
var connectionString = Environment.GetEnvironmentVariable("ACTIVITYPUB_DOTNET_STORAGE_CONNECTIONSTRING")!;

var rootCommand = new RootCommand();

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = new ContextNamePolicy(),
    WriteIndented = true
};

rootCommand.SetHandler(async (string notePath) =>
{
    var logger = loggerFactory.CreateLogger<Program>();

    logger?.LogInformation($"Reading {notePath}");

    string jsonNote = File.ReadAllText(notePath);

    var note = JsonNode.Parse(jsonNote)!.AsObject();

    var published = DateTime.UtcNow.ToString("o");
    note["published"] = published;

    var noteUri = note["id"]?.ToString();
    var attributedTo = note["attributedTo"]?.ToString();
   
    // Deserialize the JSON content into an object
    var createNote = new CreateNote() {
        Id = $"{noteUri}/create",
        Actor = attributedTo!,
        Published = published,
        Object = note
    };    
    
    var createNoteJson = JsonSerializer.Serialize(createNote, options);

    var tableClient = new TableServiceClient(connectionString);

    var followersTable = tableClient.GetTableClient("followers");

    Pageable<Follower> queryResultsFilter = followersTable.Query<Follower>();

    logger?.LogInformation($"Note to be sent: {createNoteJson}");

    var endpointsAlreadySent = new List<string>();

    var actorHelper = new ActorHelper(privateKey, keyId);
    actorHelper.Logger = logger;

    foreach (var qEntity in queryResultsFilter)
    {
        logger?.LogInformation($"Fetching follower actor information: {qEntity.ActorUri}");
        string endpointUri = string.Empty;

        try
        {
            var actor = await actorHelper.FetchActorInformationAsync(qEntity.ActorUri);
            endpointUri = actor.Endpoints?.SharedInbox ?? actor.Inbox;
        } catch (Exception ex) {
            logger?.LogError(ex.ToString());
            var actorUri = new Uri(qEntity.ActorUri);
            endpointUri = actorUri.GetLeftPart(UriPartial.Authority) + "/inbox";
        }

        logger?.LogInformation($"Inbox: {endpointUri}");

        try {
            if (endpointsAlreadySent.Contains(endpointUri))
            {
                logger?.LogInformation($"Skipping {endpointUri}");
                continue;
            }

            endpointsAlreadySent.Add(endpointUri);

            await actorHelper.SendPostSignedRequest(createNoteJson, new Uri(endpointUri));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex.ToString());
        }
    }

}, notePathOption);

rootCommand.AddOption(notePathOption);

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

class OrderedCollection
{
    [JsonPropertyName("@context")]
    public object Context { get; set; } = default!;

    public string Id { get; set; } = default!;

    public List<object> OrderedItems { get; set; } = default!;
}

class Note
{
    [JsonPropertyName("@context")]
    public object Context { get; set; } = default!;

    public string Id { get; set; } = default!;
    public string Type { get; set; } = default!;

    public string Actor { get; set; } = default!;

    public object Object { get; set; } = default!;

    public string Published { get; set; } = default!;

    public List<string> To { get; set; } = default!;
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
