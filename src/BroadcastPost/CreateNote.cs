public class CreateNote
{
    public string _context { get;set; }= "https://www.w3.org/ns/activitystreams";
    
    public string Id { get; set; } = default!;

    public string Type { get; } = "Create";

    public string Actor { get; set; } = default!;

    public string Published { get; set; } = default!;

    public List<string> To { get; } = new List<string>() {
        "https://www.w3.org/ns/activitystreams#Public"
    };

    public List<string> Cc { get; } = new List<string>() {
        "https://hachyderm.io/users/mapache/followers",
        "https://hachyderm.io/users/mapache",
    };

    public object Object { get; set; } = default!;
}