using Azure.Data.Tables;
using Azure;
using System.Text;
using System.Security.Cryptography;
using System;

namespace ActivityPubDotNet.Core.Storage
{
    public record Stamp : ITableEntity
    {
        public string RowKey { get; set; } = default!;

        public string PartitionKey { get; set; } = default!;

        public string Id { get; init; } = default!;

        public string QuotedObjectUrl { get; init; } = default!;

        public string Actor { get; init; } = default!;

        public string QuoteRequestId { get; init; } = default!;

        public ETag ETag { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; } = default!;

        public static Stamp GetFromQuoteRequest(InboxMessage quoteRequest, string stampId, string quotedObjectUrl)
        {
            var rowKey = GetMd5Hash(stampId);

            // Get the last part after the last / from the quoted object URL
            var partitionKey = quotedObjectUrl.Split('/').Last();
            
            return new Stamp()
            {
                RowKey = rowKey,
                PartitionKey = partitionKey,
                QuotedObjectUrl = quotedObjectUrl,
                Actor = quoteRequest.Actor,
                QuoteRequestId = quoteRequest.Id,
                Id = stampId
            };
        }

        static string GetMd5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();

                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2")); // Format as hexadecimal
                }

                return sb.ToString();
            }
        }
    }
}