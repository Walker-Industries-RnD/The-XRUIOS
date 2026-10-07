
using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{


    //Originally made for music, it's pretty expandable! 
    public class CreatorClass
    {
        public record Creator
        {
            public string Name;
            public string Description;
            public FileRecord? PFP;
            public List<FileRecord> Files;

            public Creator() { }

            public Creator(string name, string description, FileRecord? pfp, List<FileRecord?> files)
            {
                this.Name = name;
                this.Description = description;
                this.PFP = pfp;
                this.Files = files;
            }

        }



    }

}
