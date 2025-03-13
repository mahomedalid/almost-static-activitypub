using System.Text;
using System.Security.Cryptography;

namespace Rss2Outbox
{
    public static class RssUtils
    {
        public static string GetContent(dynamic item, string baseTagUrl, string authorName, string authorUri, string contentTemplate)
        {
            var tags = string.Empty;

            var itemTags = item?.Tags as List<string> ?? [];

            if (itemTags?.Count > 0)
            {
                foreach (var tag in item?.Tags ?? Enumerable.Empty<string>())
                {
                    tags += $" {GetHashTag(tag, $"{baseTagUrl}/{tag}")}";
                }
            }

            string description = item!.Description?.ToString().Replace("\n", "</p><p>") ?? string.Empty;

            var content = contentTemplate
                .Replace("{title}", item!.Title!)
                .Replace("{description}", description)
                .Replace("{link}", item!.Link!)
                .Replace("{author}", GetMention(authorName, authorUri))
                .Replace("{tags}", tags)
                .Replace("{content}", item!.Content ?? description);

            return content;
        }

        public static string GetHashTag(string tag, string link)
        {
            return $"<a href =\"{link}\" class=\"mention hashtag\" rel=\"tag\">#<span>{tag}</span></a>";
        }

        public static string GetMention(string name, string link)
        {
            return $"<a href=\"{link}\" class=\"u-url mention\">@<span>{name}</span></a>";
        }

        public static string GetLinkUniqueHash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to a hexadecimal string representation.
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2")); // "x2" means hexadecimal with two digits.
                }

                return sb.ToString();
            }
        }

        public static string? ParsePubDate(string? pubDate)
        {
            if (DateTimeOffset.TryParse(pubDate, out DateTimeOffset parsedDate))
            {
                return parsedDate.ToString("yyyy-MM-ddTHH:mm:sszzz");
            }

            // If parsing fails, return the original string
            return pubDate;
        }

        public static dynamic GetOutbox(string outboxUrl, IEnumerable<dynamic> orderedItems, string summary)
        {
            // Create outbox JSON structure
            var outbox = new
            {
                _context = "https://www.w3.org/ns/activitystreams",
                id = outboxUrl,
                type = "OrderedCollection",
                summary,
                totalItems = orderedItems.Count(),
                orderedItems
            };

            return outbox;
        }

        public static dynamic GetNote(dynamic item, OutboxConfig outboxConfig, string contentTemplate)
        {
            var itemHash = RssUtils.GetLinkUniqueHash(item.Link!);

            var tags = new List<NoteTag>()
            {
                new NoteTag() { Type = "Mention", Href = outboxConfig.AuthorUrl, Name = outboxConfig.AuthorUsername }
            };

            var baseTagUrl = $"{outboxConfig.Domain}/tags";

            var itemTags = item?.Tags as List<string> ?? [];

            if (itemTags?.Count > 0)
            {
                foreach (var tag in item?.Tags ?? Enumerable.Empty<string>())
                {
                    tags.Add(new NoteTag()
                    {
                        Type = "Hashtag",
                        Href = $"{baseTagUrl}/{tag}",
                        Name = $"#{tag}"
                    });
                }
            }

            var noteId = $"{outboxConfig.Domain}/{outboxConfig.NotesPath}/{itemHash}";

            var note = new
            {
                _context = "https://www.w3.org/ns/activitystreams",
                id = noteId,
                type = "Note",
                hash = itemHash,
                content = RssUtils.GetContent(item!, baseTagUrl, outboxConfig.AuthorUserId, outboxConfig.AuthorUrl, contentTemplate),
                url = item!.Link!,
                attributedTo = outboxConfig.SiteActorUri, // domain/@blog
                to = new List<string>() { "https://www.w3.org/ns/activitystreams#Public" },
                cc = new List<string>(),
                published = RssUtils.ParsePubDate(item.PubDate),
                tag = tags,
                replies = new
                {
                    id = $"{outboxConfig.Domain}/{outboxConfig.RepliesPath}/{itemHash}",
                    type = "Collection",
                    first = new
                    {
                        type = "CollectionPage",
                        next = $"{outboxConfig.Domain}/{outboxConfig.RepliesPath}/{itemHash}?page=true",
                        partOf = $"{outboxConfig.Domain}/{outboxConfig.RepliesPath}/{itemHash}",
                        items = new List<string>()
                    }
                }
            };

            return note;
        }

        public static dynamic GetCreateNote(dynamic note, OutboxConfig outboxConfig)
        {
            var createNote = new {
                _context = "https://www.w3.org/ns/activitystreams",
                id = $"{note.id}/create",
                type = "Create",
                actor = outboxConfig.SiteActorUri,
                to = new List<string>() { "https://www.w3.org/ns/activitystreams#Public" },
                cc = new List<string>(),
                published = note.published,
                @object = note
            };

            return createNote;
        }
    }
}