using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core
{
    public class AcceptRequest
    {
        [JsonPropertyName("@context")]
        public object Context { get; set; } = default!;

        public string Id { get; set; } = default!;

        public string Type { get; } = "Accept";

        public string Actor { get; set; } = default!;

        public object Object { get; set; } = default!;
    }
}
