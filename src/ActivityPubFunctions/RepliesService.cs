using ActivityPubDotNet.Core;
using ActivityPubDotNet.Core.Storage;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ActivityPubDotNet
{
    public class RepliesService(TableServiceClient tableServiceClient, RepliesGenerator repliesGenerator, string domain)
    {
        private TableServiceClient _tableServiceClient = tableServiceClient;

        private RepliesGenerator _repliesGenerator = repliesGenerator;

        private ILogger? _logger = default!;

        private static readonly string RepliesTable = "replies";

        public string Domain { get; set; } = domain;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task AddReply(InboxMessage message)
        {
            _logger?.LogDebug($"Creating table {RepliesTable}");

            await _tableServiceClient.CreateTableIfNotExistsAsync(RepliesTable);

            var objectNote = JsonSerializer.Deserialize<Note>(JsonSerializer.Serialize(message!.Object!, SerializerOptions), SerializerOptions);

            // Maybe is a reply (aka a comment)
            if (!objectNote!.InReplyTo?.StartsWith(Domain) ?? true)
            {
                // We don't do anything
                return;
            }

            var reply = Reply.GetFromNote(objectNote);

            var repliesTable = _tableServiceClient.GetTableClient(RepliesTable);

            _logger?.LogDebug($"Adding follower");

            await repliesTable.AddEntityAsync(reply);

            await _repliesGenerator.Generate(reply.NoteId);
        }
    }
}
