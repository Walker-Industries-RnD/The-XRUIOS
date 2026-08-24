
using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    public static class DataManagerClass
    {

        //Worldpoints are data handling all things from console to 2D to 3D

        //Dataslots are a chunk of reality, 2D or 3D, filled with tons of Worldpoints by GUID

        //Sessions ARE reality, created by connecting dataslots by GUID


        //Example; we want to run a "Work" session, which will fill stuff at home and a friends house
        //You can go to these places and switch the specfic stuff happening VIA dataslot

        public static class SessionClass
        {

            public record Session
            {
                public DateTime Created; //The date and time created, UtcNow
                public string Title; //Title
                public string Description; //Description
                public List<string> WorldPointIdentifiers;
                public string Identifier;

                //Logo exists in the same path

                public Session() { }


                public Session(DateTime? CreatedDateTime, string Title, string Description, List<string> identifiers, string? identifier)
                {
                    if (CreatedDateTime == null)
                    {
                        CreatedDateTime = DateTime.Now;
                    }

                    else
                    {
                        this.Created = (DateTime)CreatedDateTime;
                    }

                    this.Title = Title;
                    this.Description = Description;
                    this.WorldPointIdentifiers = identifiers;

                    Identifier = identifier ?? Guid.NewGuid().ToString();

                }
            }




            //Update method
            public record SessionPatch
            {
                public DateTime? Created { get; init; }             // Optional: only update if not null
                public string? Title { get; init; }                 // Optional
                public string? Description { get; init; }           // Optional
                public List<string>? WorldPointIdentifiers { get; init; }  // Optional
            }








        }


        public static class DataSlotClass
        {

            public record DataSlot
            {
                public bool IsFavorite; //If this is favorited
                public DateTime DateAndTime; //The date and time it was made
                public string Title; //Title
                public string Description; //Description
                public FileRecord ImgPath; //The path to the img icon
                public FileRecord TextureFolder; //2.5D images for previewing, for v2
                public List<string> Sessions; //GUIDs
                public string Identifier;

                public DataSlot() { }
                public DataSlot(
                    bool isFavorite,
                    DateTime? dateTimeVar, // nullable, may be default
                    string title,
                    string description,
                    FileRecord imgPath,
                    FileRecord? textureFolder, // optional for now
                    List<string> structSessions,
                    string? identifier)
                {
                    IsFavorite = isFavorite;

                    this.DateAndTime = dateTimeVar ?? DateTime.UtcNow;

                    Title = title;
                    Description = description;
                    ImgPath = imgPath;
                    TextureFolder = textureFolder ?? default; // still allows null/default
                    Sessions = structSessions ?? new List<string>();
                    Identifier = identifier ?? Guid.NewGuid().ToString();

                }

            }

            public record DataSlotPatch
            {
                public bool? IsFavorite { get; init; }
                public DateTime? DateAndTime { get; init; }
                public string? Title { get; init; }
                public string? Description { get; init; }
                public FileRecord? ImgPath { get; init; }
                public FileRecord? TextureFolder { get; init; }
                public List<string>? Sessions { get; init; }
            }

            public static DataSlot UpdateDataSlot(DataSlot slot, DataSlotPatch patch)
            {
                return new DataSlot(
                    patch.IsFavorite ?? slot.IsFavorite,
                    patch.DateAndTime ?? slot.DateAndTime,
                    patch.Title ?? slot.Title,
                    patch.Description ?? slot.Description,
                    patch.ImgPath ?? slot.ImgPath,
                    patch.TextureFolder ?? slot.TextureFolder,
                    patch.Sessions ?? slot.Sessions,
                    slot.Identifier // always keep original
                );
            }




        }




    }
}

