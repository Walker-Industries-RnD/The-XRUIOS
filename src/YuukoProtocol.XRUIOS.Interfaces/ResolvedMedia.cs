using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YuukoProtocol
{
    public sealed record ResolvedMedia(string FullPath, string FileName, string DirectoryUuid, long SizeBytes, DateTime LastModifiedUtc);

}
