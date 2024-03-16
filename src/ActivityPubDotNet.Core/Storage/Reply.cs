using Azure.Data.Tables;
using Azure;
using System.Text;
using System.Security.Cryptography;
using System;

namespace ActivityPubDotNet.Core.Storage
{
    public record Reply : ITableEntity
    {
        public string RowKey { get; set; } = default!;

        public string PartitionKey { get; set; } = default!;

        public string Id { get; init; } = default!;

        public string NoteId { get; init; } = default!;

        public ETag ETag { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; } = default!;

        public static Reply GetFromNote(Note note)
        {
            var rowKey = GetMd5Hash(note.Id);

            // Get the last part after the last /  from note.InReplyTo
            var partitionKey = note.InReplyTo.Split('/').Last();
            
            return new Reply()
            {
                RowKey = rowKey,
                PartitionKey = partitionKey,
                NoteId = note.InReplyTo,
                Id = note.Id
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
