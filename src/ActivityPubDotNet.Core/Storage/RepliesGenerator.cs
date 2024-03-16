using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ActivityPubDotNet.Core.Storage
{
    public class RepliesGenerator(TableServiceClient tableServiceClient, BlobServiceClient blobServiceClient)
    {
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

        private readonly TableServiceClient tableServiceClient = tableServiceClient;

        private readonly BlobServiceClient blobServiceClient = blobServiceClient;

        private const string RepliesTable = "replies";

        public string Domain { get; set; } = default!;

        public async Task Generate(string noteUrl)
        {
            var repliesTable = tableServiceClient.GetTableClient(RepliesTable);

            // get note from msg
            var noteId = noteUrl.Split('/').Last();

            // query replies
            var repliesQuery = repliesTable.QueryAsync<Reply>(filter: $"PartitionKey eq '{noteId}'");

            var replies = new List<string>();

            await foreach (var reply in repliesQuery)
            {
                replies.Add(reply.Id);
            }

            var repliesPage = new
            {
                _context = "https://www.w3.org/ns/activitystreams",
                // generate Id 
                id = $"{Domain!}/socialweb/replies/{noteId}?page=true",
                partOf = $"{Domain!}/socialweb/replies/{noteId}",
                type = "CollectionPage",
                items = replies
            };

            //Store into a blob in the container $web, and path "/socialweb/replies/{noteId}"

            var containerClient = blobServiceClient.GetBlobContainerClient("$web");

            // Serialize the repliesPage object to JSON
            string jsonContent = JsonSerializer.Serialize(repliesPage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = new ContextNamePolicy(),
                WriteIndented = true
            });

            // Generate blob name using MD5 hash of the noteUrl
            string blobName = $"socialweb/replies/{noteId}";

            // Get a reference to the blob
            var blobClient = containerClient.GetBlobClient(blobName);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/activity+json" // Set your custom content type here
            };

            // Upload the JSON content to the blob
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent)))
            {
                await blobClient.UploadAsync(stream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = blobHttpHeaders,
                        Conditions = default
                    });
            }
        }
    }
}
