using ActivityPubDotNet.Core;
using ActivityPubDotNet.Core.Storage;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ActivityPubDotNet
{
    public class ServerConfig
    {
        public string BaseDomain { get; set; } = default!;

        public string ActorName { get; set; } = "blog";
    }
}
