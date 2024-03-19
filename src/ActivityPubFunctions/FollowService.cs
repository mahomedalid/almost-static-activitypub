﻿using ActivityPubDotNet.Core;
using ActivityPubDotNet.Core.Storage;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet
{
    public class FollowService(TableServiceClient tableServiceClient, ActorHelper actorHelper)
    {
        private readonly ILogger? _logger = default;

        private readonly string FollowersTable = "followers";

        private readonly TableServiceClient _tableServiceClient = tableServiceClient;

        private readonly ActorHelper _actorHelper = actorHelper;

        public async Task Follow(InboxMessage message)
        {
            await CreateFollower(message);
            await SendAcceptedFollowRequest(message);
        }

        public async Task Unfollow(InboxMessage message)
        {
            await DeleteFollower(message);
        }

        public async Task CreateFollower(InboxMessage message)
        {
            await _tableServiceClient.CreateTableIfNotExistsAsync(FollowersTable);

            _logger?.LogDebug($"Follow request from: {message.Actor}");
            
            var follower = Follower.GetFromMessage(message);

            var followersTable = _tableServiceClient.GetTableClient(FollowersTable);

            _logger?.LogDebug($"Searching for existing follower {follower.PartitionKey} {follower.RowKey}");

            try
            {
                var existing = await followersTable.GetEntityAsync<Follower>(follower.PartitionKey, follower.RowKey);

                if (existing != null)
                {
                    _logger?.LogDebug($"Follower already exists");
                }
            }
            catch (Azure.RequestFailedException e)
            {
                _logger?.LogDebug($"Adding follower, it does not exists: {e}");

                await followersTable.AddEntityAsync(follower);
            }
        }

        public async Task DeleteFollower(InboxMessage message)
        {
            await _tableServiceClient.CreateTableIfNotExistsAsync(FollowersTable);

            _logger?.LogDebug($"Unfollow request from: {message.Actor}");

            var follower = Follower.GetFromMessage(message);

            var followersTable = _tableServiceClient.GetTableClient(FollowersTable);

            _logger?.LogDebug($"Searching for existing follower {follower.PartitionKey} {follower.RowKey}");

            try
            {
                var existing = await followersTable.GetEntityAsync<Follower>(follower.PartitionKey, follower.RowKey);

                // Update or DELETE?
                await followersTable.DeleteEntityAsync(follower.PartitionKey, follower.RowKey);
            }
            catch (Azure.RequestFailedException e)
            {
                _logger?.LogDebug($"Follower does not exists in our table, or could not be deleted {e}");
            }
        }

        public async Task<AcceptRequest> SendAcceptedFollowRequest(InboxMessage message)
        {
            // Target is the account to be followed
            var target = message.Object!.ToString();

            // Actor is the account who wants to follow
            var actor = await ActorHelper.FetchActorInformationAsync(message.Actor);

            _logger?.LogInformation($"Actor: {actor.Id} - {actor.Name} - {actor.Url} => Target: {target}");

            //'#accepts/follows/'
            var acceptRequest = new AcceptRequest()
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = $"{target}#accepts/follows/{actor.Id}",
                Actor = $"{target}",
                Object = new
                {
                    message.Id,
                    Actor = actor.Url,
                    Type = "Follow",
                    Object = $"{target}"
                }
            };

            await _actorHelper.SendSignedRequest(acceptRequest, new Uri(actor.Inbox), _logger);

            return acceptRequest;
        }
    }
}