using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    public static class NoteClass
    {

        public record Note
        {
            // New fields
            public string Title;
            public string Category;
            public DateTime Created;
            public DateTime LastUpdate;
            public string SavedID;
            public string MiniDescription;
            public string NoteID;
            public string XRUIOSNoteID;
            public FileRecord Markdown;
            public List<FileRecord>? Images;

            public Note() { }

            // Constructor
            public Note(string title, string category, DateTime created, DateTime lastUpdate, string savedID, string miniDescription, string noteID, string xRUIOSNoteID, FileRecord markdown, List<FileRecord>? images)
            {
                // Assign new fields
                Title = title;
                Category = category;
                Created = created;
                LastUpdate = lastUpdate;
                SavedID = savedID;
                MiniDescription = miniDescription;
                
                // Assign existing fields
                NoteID = noteID;
                XRUIOSNoteID = xRUIOSNoteID;
                Markdown = markdown;
                Images = images;
            }
        }


        public record Journal
        {
            // New fields
            public string JournalName;
            public string Description;
            public string CoverImagePath;
            public ThemeIdentity Identity;

            public List<Category> Categories; //We treat as chapters

            public Journal() { }


            // Constructor
            public Journal(string journalName, string description, string coverImagePath, List<Category> categories, ThemeIdentity identity)
            {
                JournalName = journalName;
                Description = description;
                CoverImagePath = coverImagePath;
                Identity = identity;

                Categories = categories;
            }
        }

        public record Category
        {

            public string Title;
            public string Description;
            public string MainImage;
            public string MiniImage;

            public List<FileRecord> Notes; //We treat order as pages
            public Category() { }

            public Category(string title, string description, string mainImage, string miniImage, List<FileRecord> notes)
            {

                Title = title;
                Description = description;
                MainImage = mainImage;
                MiniImage = miniImage;


                Notes = notes;
            }
        }

        public record ThemeIdentity
        {
            public string ThemeID;
            public string Name;
            public string Author;
            public string Version;
            public List<string> TargetModes;


            public ThemeIdentity() { }

            public ThemeIdentity(
                string themeID,
                string name,
                string author,
                string version,
                List<string> targetModes)
            {
                ThemeID = themeID;
                Name = name;
                Author = author;
                Version = version;
                TargetModes = targetModes;

            }
        }

     
        //History
        public record HistoryEntry
        {
            public string Action;          // Created, Edited, Viewed, Favorited
            public string TargetType;      // Journal, Music, App
            public string TargetID;        // UUID / Identifier
            public DateTime Timestamp;     // UTC
            public Dictionary<string, string>? Meta;

            public HistoryEntry() { }

            public HistoryEntry(
                string action,
                string targetType,
                string targetID,
                DateTime timestamp,
                Dictionary<string, string>? meta = null)
            {
                Action = action;
                TargetType = targetType;
                TargetID = targetID;
                Timestamp = timestamp;
                Meta = meta;
            }
        }

        

    }

}

