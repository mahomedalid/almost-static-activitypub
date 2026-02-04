using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ActivityPubDotNet.Core.Storage
{
    public class StampsGenerator(BlobServiceClient blobServiceClient)
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

        private readonly BlobServiceClient blobServiceClient = blobServiceClient;

        public string Domain { get; set; } = default!;

        public async Task GenerateStampFile(string stampId, string actor, string quoteStatusUrl, string quotedPostUrl)
        {
            // Create individual stamp authorization
            var stampAuthorization = new
            {
                _context = new object[]
                {
                    "https://www.w3.org/ns/activitystreams",
                    new Dictionary<string, object>
                    {
                        {"QuoteAuthorization", "https://w3id.org/fep/044f#QuoteAuthorization"},
                        {"gts", "https://gotosocial.org/ns#"},
                        {"interactingObject", new Dictionary<string, object>
                            {
                                {"@id", "gts:interactingObject"},
                                {"@type", "@id"}
                            }
                        },
                        {"interactionTarget", new Dictionary<string, object>
                            {
                                {"@id", "gts:interactionTarget"},
                                {"@type", "@id"}
                            }
                        }
                    }
                },
                type = "QuoteAuthorization",
                id = stampId,
                attributedTo = actor,
                interactingObject = quoteStatusUrl,
                interactionTarget = quotedPostUrl
            };

            //Store into a blob in the container $web
            var containerClient = blobServiceClient.GetBlobContainerClient("$web");

            // Serialize the stamp authorization object to JSON
            string jsonContent = JsonSerializer.Serialize(stampAuthorization, new JsonSerializerOptions
            {
                PropertyNamingPolicy = new ContextNamePolicy(),
                WriteIndented = true
            });

            // Extract the stamp ID path from the full URL
            var stampUri = new Uri(stampId);
            string blobName = stampUri.AbsolutePath.TrimStart('/');

            // Get a reference to the blob
            var blobClient = containerClient.GetBlobClient(blobName);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/activity+json"
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