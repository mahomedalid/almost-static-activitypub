using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ActivityPubDotNet.Core
{
    public class Actor
    {
        public class PublicKeyDefinition
        {
            [JsonPropertyName("@context")]
            public object? Context { get; set; } = default!;
            public string? Type { get; set; } = default!;
            public string Id { get; set; } = default!;
            public string Owner { get; set; } = default!;
            public string PublicKeyPem { get; set; } = default!;
        }

        [JsonPropertyName("@context")]
        public object Context { get; set; } = default!;

        public string Id { get; set; } = default!;
        public string Type { get; set; } = default!;
        
        public string Outbox { get; set; } = default!;
        public string Following { get; set; } = default!;
        public string Followers { get; set; } = default!;
        public string Inbox { get; set; } = default!;
        public string PreferredUsername { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Summary { get; set; } = default!;
        public string Url { get; set; } = default!;
        public PublicKeyDefinition PublicKey { get; set; } = default!;
    }
}
