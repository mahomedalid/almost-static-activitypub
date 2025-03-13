using System.Text;
using System.Numerics;
using System.Security.Cryptography;

namespace Rss2Outbox
{
    public class OutboxConfig
    {
        public string AuthorUrl { get; set; } = default!;

        public string AuthorUsername { get; set; } = default!;

        public string Domain { get; set; } = default!;

        public string NotesPath { get; set; } = default!;

        public string OutboxPath { get; set; } = default!;

        public string StaticPath {get; set; } = default!;

        public string SiteActorUri { get; set; } = default!;

        public string RepliesPath { get; set; } = default!;

        public string NotesFullPath { get { return Path.Combine(StaticPath, NotesPath); } }

        public string OutputFullPath { get { return Path.Combine(StaticPath, OutboxPath); } }

        public string AuthorUserId { get; set; } = default!;

        public string OutboxUrl { get { return $"{Domain}/{OutboxPath}"; } }
    }
}

