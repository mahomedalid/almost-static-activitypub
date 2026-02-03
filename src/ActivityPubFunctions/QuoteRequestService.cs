using ActivityPubDotNet.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityPubDotNet
{
    public class QuoteRequestService(TableServiceClient tableServiceClient, ActorHelper actorHelper, ServerConfig serverConfig)
    {
        private readonly TableServiceClient _tableServiceClient = tableServiceClient;
        private readonly ActorHelper _actorHelper = actorHelper;
        private readonly ServerConfig _serverConfig = serverConfig;

        public ILogger? Logger { get; set; }

        public async Task ProcessQuoteRequest(InboxMessage message)
        {
            if (message == null || !message.IsQuoteRequest())
            {
                Logger?.LogWarning("Invalid quote request message");
                return;
            }

            try
            {
                Logger?.LogInformation($"Processing quote request from {message.Actor} for object {message.Object}");

                // For automatic approval, we'll immediately accept all quote requests
                // In a production system, you might want to add filtering logic here
                await AutoApproveQuoteRequest(message);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error processing quote request");
            }
        }

        private async Task AutoApproveQuoteRequest(InboxMessage message)
        {
            try
            {
                // Get the object URL being quoted
                var noteUrl = message.Object?.ToString();
                if (string.IsNullOrEmpty(noteUrl))
                {
                    Logger?.LogWarning("Quote request missing object URL");
                    return;
                }

                // Find the actor that owns the quoted object
                var actor = GetActorForQuotedObject(noteUrl);
                if (actor == null)
                {
                    Logger?.LogWarning($"Could not find actor for quoted object: {noteUrl}");
                    return;
                }

                // Create Accept response
                var objectId = Guid.NewGuid().ToString();
                var quoteId = $"https://{_serverConfig.BaseDomain}/activities/accept/{objectId}";
                var stampId = $"https://{_serverConfig.BaseDomain}/stamps/{objectId}";

                var acceptResponse = new QuoteAcceptResponse
                {
                    Context = new object[]
                    {
                        "https://www.w3.org/ns/activitystreams",
                        new Dictionary<string, object>
                        {
                            {"QuoteRequest", "https://w3id.org/fep/044f#QuoteRequest"}
                        }
                    },
                    Type = "Accept",
                    Id = quoteId,
                    Actor = $"{_serverConfig.BaseDomain}/{_serverConfig.ActorName}",
                    To = message.Actor,
                    Object = message,
                    Result = stampId // This acts as the QuoteAuthorization
                };

                // Send the Accept response
                await SendAcceptResponse(acceptResponse, message.Actor);

                Logger?.LogInformation($"Sent Accept response to {message.Actor} for quote request {message.Id}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error auto-approving quote request");
            }
        }

        private QuoteActor? GetActorForQuotedObject(string noteUrl)
        {
            // For our static site, all objects belong to our blog actor
            if (noteUrl.Contains(_serverConfig.BaseDomain))
            {
                return new QuoteActor
                {
                    Domain = _serverConfig.BaseDomain,
                    Uri = new Uri($"{_serverConfig.BaseDomain}/{_serverConfig.ActorName}")
                };
            }

            return null;
        }

        private async Task SendAcceptResponse(QuoteAcceptResponse acceptResponse, string actorUri)
        {
            try
            {
                Logger?.LogDebug($"Fetching actor information from {actorUri}");
                var actor = await _actorHelper.FetchActorInformationAsync(actorUri);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                var document = JsonSerializer.Serialize(acceptResponse, options);

                Logger?.LogInformation($"Sending accept response to {actor.Inbox} - {document}");

                await _actorHelper.SendPostSignedRequest(document, new Uri(actor.Inbox));
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"Error sending accept response to {actorUri}");
            }
        }
    }

    public class QuoteAcceptResponse
    {
        [JsonPropertyName("@context")]
        public object? Context { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;

        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("actor")]
        public string? Actor { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("object")]
        public object? Object { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }
    }

    public class QuoteActor
    {
        public string Domain { get; set; } = default!;
        public Uri? Uri { get; set; }
    }
}