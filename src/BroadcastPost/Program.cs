﻿using System.Text.Json;
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

var serviceCollection = new ServiceCollection();

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
            var actor = await ActorHelper.FetchActorInformationAsync(qEntity.ActorUri);
            endpointUri = actor.Endpoints.SharedInbox ?? actor.Inbox;
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

            await actorHelper.SendSignedRequest(createNoteJson, new Uri(endpointUri));
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

static string GetNoteId(string notePath)
{
    // Split the notePath by "/"
    string[] parts = notePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
    
    // Check if the notePath has at least one "/"
    if (parts.Length > 0)
    {
        // Return the last part of the notePath
        return parts[parts.Length - 1];
    }
    else
    {
        // If there are no "/", return the whole notePath
        return notePath;
    }
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
