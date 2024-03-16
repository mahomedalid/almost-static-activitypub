using System.Text.Json.Serialization;

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

        public class EndpointsDefinition
        {
            public string? SharedInbox { get; set; } = default!;
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

        public EndpointsDefinition Endpoints { get; set; } = default!;
    }
}
