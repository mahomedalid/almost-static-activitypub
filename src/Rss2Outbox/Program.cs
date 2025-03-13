using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml.Linq;
using System.CommandLine.Parsing;
using System.CommandLine;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rss2Outbox;

var serviceCollection = new ServiceCollection();

ConfigureServices(serviceCollection, args);

using var serviceProvider = serviceCollection.BuildServiceProvider();

var inputOption = new Option<string>("--rssPath")
{
    IsRequired = true,
    Description = "The path to the RSS feed, usually a local path to index.xml"
};

var staticPathOption = new Option<string>("--staticPath")
{
    IsRequired = true,
    Description = "The path to the static folder where the outbox and notes will be generated"
};

var siteActorUriOption = new Option<string>("--siteActorUri")
{
    IsRequired = true,
    Description = "The uri of the author (actor endpoint) of the blog, ex. https://maho.dev/@blog"
};

var authorUsernameOption = new Option<string>("--authorUsername")
{
    IsRequired = true,
    Description = "The author username if the human publishing the blog, ex. @mapache@hachyderm.io"
};

var domainOption = new Option<string>("--domain")
{
    IsRequired = false,
    Description = "The domain of the blog, if not provided it will be extracted from the RSS feed"
};

var authorUriOption = new Option<string>("--authorUri")
{
    IsRequired = false,
    Description = "The author uri if the human publishing the blog, ex. https://hachyderm.io/users/mapache. If not provider, guessed from the authorUsername."
};

var rootCommand = new RootCommand();

var loggerFactory = serviceProvider.GetService<ILoggerFactory>()!;

rootCommand.SetHandler((input, staticPath, authorUsername, siteActorUri, domain, authorUri) => {
    var logger = loggerFactory.CreateLogger<Program>();

    // Split authorUsername (ex. @mapache@hachyderm.io) by @, the first element is the username and the second is the domain
    // TODO: Throw exception if the authorUsername does nto have two @ characters and authorUri is null or empty
    string[] parts = authorUsername.Split('@');
    
    // First element is the username and second is the domain
    string authorUserId = parts[1];
    string authorDomain = parts[2];
    
    var config = new OutboxConfig()
    {
        StaticPath = staticPath,
        Domain = domain,
        AuthorUrl = authorUri ?? $"https://{authorDomain}/users/{authorUserId}",
        AuthorUsername = authorUsername,
        NotesPath = "socialweb/notes",
        OutboxPath = "socialweb/outbox",
        RepliesPath = "socialweb/replies",
        AuthorUserId = authorUserId,
        SiteActorUri = siteActorUri
    };

    GenerateOutbox(logger, input, config);
}, inputOption, staticPathOption, authorUsernameOption, siteActorUriOption, domainOption, authorUriOption);

rootCommand.AddOption(inputOption);
rootCommand.AddOption(staticPathOption);
rootCommand.AddOption(authorUsernameOption);
rootCommand.AddOption(siteActorUriOption);
rootCommand.AddOption(domainOption);
rootCommand.AddOption(authorUriOption);

var result = await rootCommand.InvokeAsync(args);

return result;

static void GenerateOutbox(ILogger logger, string input, OutboxConfig config)
{
    logger?.LogInformation($"Reading rss {input}, generating outbox {config.OutputFullPath} and notes in {config.NotesFullPath} for domain {config.Domain}");

    // Parse RSS XML
    XDocument rssXml = XDocument.Parse(input);

    XNamespace dc = "http://purl.org/dc/elements/1.1/";
    // Extract items from RSS XML
    var items = rssXml.Descendants("item")
                    .Select(item => new
                    {
                        Title = item.Element("title")?.Value ?? item.Element("description")?.Value,
                        Link = item.Element("link")?.Value,
                        Description = item.Element("description")?.Value,
                        Content = item.Element("{http://purl.org/rss/1.0/modules/content/}encoded")?.Value ?? item.Element("description")?.Value,
                        PubDate = item.Element("pubDate")?.Value,
                        Author = item.Element(dc + "creator")?.Value
                    });
        
    // Get summary from the rss
    var summary = rssXml.Descendants("channel")
        .Select(channel => channel.Element("description")?.Value)
        .FirstOrDefault();

    var orderedItems = new List<dynamic>();

    // Create the folder config.NotesFullPath if does not exists
    if (!Directory.Exists(config.NotesFullPath))
    {
        Directory.CreateDirectory(config.NotesFullPath);
    }
    
    foreach (var item in items)
    {
        var note = RssUtils.GetNote(item, config);
        var createNote = RssUtils.GetCreateNote(note, config);
       
        orderedItems.Add(createNote);
        
        string noteJson = JsonSerializer.Serialize(note, new JsonSerializerOptions
        {
            PropertyNamingPolicy = new ContextNamePolicy(),
            WriteIndented = true
        });

        File.WriteAllText(Path.Combine(config.NotesFullPath, note.hash), noteJson);
    }

    var outbox = RssUtils.GetOutbox(config.OutboxUrl, orderedItems, summary ?? $"Outbox for {config.AuthorUsername} blog");
    
    // Serialize outbox to JSON
    string outboxJson = JsonSerializer.Serialize(outbox, new JsonSerializerOptions
    {
        PropertyNamingPolicy = new ContextNamePolicy(),
        WriteIndented = true
    });

    // Write JSON to file
    File.WriteAllText(config.OutputFullPath, outboxJson);

    logger?.LogInformation("Outbox JSON created successfully.");
}

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

        return name;
    }
}

internal struct NoteTag
{
    public string Type { get; set; }
    public string Href { get; set; }
    public string Name { get; set; }
}