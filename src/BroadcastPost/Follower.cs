using Azure.Data.Tables;
using Azure;
using System.Text;
using System.Security.Cryptography;

namespace ActivityPubDotNet
{
    public record Follower : ITableEntity
    {
        public string RowKey { get; set; } = default!;

        public string PartitionKey { get; set; } = default!;

        public string ActorUri { get; init; } = default!;

        public ETag ETag { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; } = default!;

        public string? LastError { get; set; }

        public DateTimeOffset? LastErrorDate { get; set; }

        public DateTimeOffset? LastSuccessDate { get; set; }

        public static Follower GetFromMessage(InboxMessage msg)
        {
            var rowKey = GetMd5Hash(msg.Actor);

            Uri uri = new (msg.Actor);

            return new Follower()
            {
                RowKey = rowKey,
                PartitionKey = uri.Host,
                ActorUri = msg.Actor
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
