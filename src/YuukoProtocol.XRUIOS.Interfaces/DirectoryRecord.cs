using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YuukoProtocol
{
    public record DirectoryRecord
    {
        public string UUID { get; set; }
        public string PathName { get; set; }
        public string Path { get; set; }

        public DirectoryRecord() { }

        public DirectoryRecord(string uuid, string pathName, string path)
        {
            UUID = uuid;
            PathName = pathName;
            Path = path;
        }
    }

}
