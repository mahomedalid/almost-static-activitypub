namespace ActivityPubDotNet.Core
{
    public class Note
    {
        public string Id { get; set; } = default!;

        public string Type { get; set; } = default!;

        public string InReplyTo { get; set; } = default!;
    }
}
