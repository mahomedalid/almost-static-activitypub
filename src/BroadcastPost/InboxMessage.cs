using System.Text.Json.Serialization;

namespace ActivityPubDotNet
{
    public class InboxMessage
    {
        [JsonPropertyName("@context")]
        public object? Context { get; set; }
        public string Actor { get; set; } = default!;
        public List<string>? Cc { get; set; }
        public string? Id { get; set; }
        public object? Object { get; set; }
        public DateTime? Published { get; set; }
        public string? State { get; set; }
        public List<string>? To { get; set; }
        public string Type { get; set; } = default!;
    }
}
