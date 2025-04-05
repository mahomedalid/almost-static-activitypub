using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ActivityPubDotNet.Core.Storage
{
    public class FollowersGenerator(TableServiceClient tableServiceClient, BlobServiceClient blobServiceClient)
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

        private const string FollowersTable = "followers";

        public string Domain { get; set; } = default!;

        public async Task Generate()
        {
            var table = tableServiceClient.GetTableClient(FollowersTable);

            var query = table.QueryAsync<Follower>();

            var items = new List<string>();

            await foreach (var item in query)
            {
                items.Add(item.ActorUri);
            }

            var repliesPage = new
            {
                _context = "https://www.w3.org/ns/activitystreams",
                // generate Id 
                id = $"{Domain!}/socialweb/followers",
                type = "CollectionPage",
                totalItems = items.Count(),
                partOf = $"{Domain!}/socialweb/followers",
                items
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
            string blobName = $"socialweb/followers";

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
