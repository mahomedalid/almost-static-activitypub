namespace BoostPost
{
    public class AnnounceActivity
    {
        public string _context { get; set; } = "https://www.w3.org/ns/activitystreams";

        public string Id { get; set; } = default!;

        public string Type { get; } = "Announce";

        public string Actor { get; set; } = default!;

        public string Published { get; set; } = default!;

        public List<string> To { get; set; } = new()
        {
            "https://www.w3.org/ns/activitystreams#Public"
        };

        public List<string> Cc { get; set; } = new();

        public string Object { get; set; } = default!;
    }
}
