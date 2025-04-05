using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ActivityPubDotNet.Core;

namespace ActivityPubDotNet
{
    public class Inbox(ILoggerFactory loggerFactory, FollowService followService, RepliesService repliesService, ActorHelper actorHelper, ServerConfig serverConfig)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<Inbox>();

        private readonly FollowService _followService = followService;

        private readonly RepliesService _repliesService = repliesService;

        private readonly ServerConfig _serverConfig = serverConfig;

        private readonly ActorHelper _actorHelper = actorHelper;

        [Function("Inbox")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            InboxMessage? message;

            try
            {
                message = JsonSerializer.Deserialize<InboxMessage>(requestBody, options);
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                throw;
            }

            if (message?.IsDelete() ?? false)
            {
                throw new NotImplementedException("Delete not supported");
            }

            _logger.LogInformation($"Received Activity: {requestBody}");

            var response = req.CreateResponse(HttpStatusCode.OK);

            response.Headers.Add("Content-Type", "application/activity+json");

            try
            {
                _followService.Logger = _logger;
                _actorHelper.Logger = _logger;

                if (message?.IsFollow() ?? false)
                {
                    await _followService.Follow(message);

                    await _followService.UpdateFollowersCollection();
                }
                else if (message?.IsUndoFollow() ?? false)
                {
                    await _followService.Unfollow(message);

                    _logger.LogDebug($"Fetching actor information from {message.Actor}");
                    var actor = await _actorHelper.FetchActorInformationAsync(message.Actor);

                    _logger?.LogInformation($"Actor: {actor.Id} - {actor.Name} - {actor.Url}");

                    var uuid = Guid.NewGuid();

                    var acceptRequest = new AcceptRequest()
                    {
                        Context = "https://www.w3.org/ns/activitystreams",
                        Id = $"{_serverConfig.BaseDomain}/{uuid}",
                        Actor = $"{_serverConfig.BaseDomain}/{_serverConfig.ActorName}",
                        Object = JsonSerializer.Deserialize<dynamic>(requestBody, options)!
                    };

                    var document = JsonSerializer.Serialize(acceptRequest, options);

                    _logger?.LogInformation($"Sending accept request to {actor.Inbox} - {document}");

                    await _actorHelper.SendPostSignedRequest(document, new Uri(actor.Inbox));

                    await _followService.UpdateFollowersCollection();
                }
                else if (message?.IsCreateActivity() ?? false)
                {
                    await _repliesService.AddReply(message);
                }
                else
                {
                    // TODO: Bad Request
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                throw;
            }

            return response;
        }
    }
}
