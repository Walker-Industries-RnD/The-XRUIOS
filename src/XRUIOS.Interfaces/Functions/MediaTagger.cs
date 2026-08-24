using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    internal class MediaTagger
    {

        //Was specfically made for Images and Videos but can be used for anything, uses CRUD format
        //This is actually taken from CreatorClass, which had a lot of what we needed!

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
                    this.Files = files ?? new List<FileRecord>();
                }

            }

        }

    }
}
