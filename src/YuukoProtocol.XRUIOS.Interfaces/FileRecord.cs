using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YuukoProtocol
{
    public record FileRecord
    {
        public string UUID { get; set; }
        public string File { get; set; }

        public FileRecord() { }

        public FileRecord(string? uuid, string file)
        {
            UUID = uuid ?? Guid.NewGuid().ToString();
            File = file;
        }
    }

}
