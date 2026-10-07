using System.Text.RegularExpressions;
using EclipseProject;
using XRUIOS.Barebones.Functions;
using XRUIOS.Interpreter;
using NoteTypes = XRUIOS.Barebones.Interfaces.NoteClass;

namespace XRUIOS.Worker.Note
{
    // Journals: a journal is a book with categories (chapters). This surface lists, creates and deletes
    // journals, shows a journal's categories, and keeps a favorites list. Editing the notes inside a
    // category is not exposed yet.
    //
    // A journal's id is its file name, "<name> v<version> by <author>__ID <id>". It is built from text the
    // caller sends, so every id and name is checked before it can reach the file system.
    public static class NoteCapabilities
    {
        private static readonly Regex SafeText = new(@"^[\p{L}\p{N}][\p{L}\p{N} ._'-]{0,59}$", RegexOptions.Compiled);
        private static readonly Regex SafeId = new(@"^[\p{L}\p{N}][\p{L}\p{N} ._'-]{0,199}$", RegexOptions.Compiled);
        private const string FavoritesFile = "JournalFavorites";

        private static string JournalDir => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Journals");

        [SeaOfDirac("Note.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Note: " + input;

        /// <summary>"id|name|author|version|description|categories;..."</summary>
        [SeaOfDirac("ListJournals", new string[0], typeof(string))]
        public static async Task<string> List()
        {
            var journals = await NoteClass.GetAllJournals();
            return string.Join(';', journals.Select(j =>
                $"{IdOf(j)}|{Wire.Field(j.JournalName)}|{Wire.Field(j.Identity.Author)}|{Wire.Field(j.Identity.Version)}|{Wire.Field(j.Description)}|{j.Categories?.Count ?? 0}"));
        }

        /// <summary>Create an empty journal. Returns its id.</summary>
        [SeaOfDirac("CreateJournal", new[] { "name", "author", "description" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Create(string name, string author, string description = "")
        {
            CheckText(name, "name");
            CheckText(author, "author");

            var identity = new NoteTypes.ThemeIdentity(Guid.NewGuid().ToString("N"), name, author, "1", new List<string>());
            var journal = new NoteTypes.Journal(name, description ?? "", "", new List<NoteTypes.Category>(), identity);
            string id = IdOf(journal);
            if (File.Exists(Path.Combine(JournalDir, id + ".json")))
                throw new InvalidOperationException("This journal already exists; please change the name.");

            await NoteClass.SaveJournal(journal);
            return id;
        }

        /// <summary>"title|description|notes;..." for the categories (chapters) of a journal.</summary>
        [SeaOfDirac("ListJournalCategories", new[] { "journalId" }, typeof(string), typeof(string))]
        public static async Task<string> Categories(string journalId)
        {
            CheckId(journalId);
            var journal = await Load(journalId);
            return string.Join(';', (journal.Categories ?? new()).Select(c =>
                $"{Wire.Field(c.Title)}|{Wire.Field(c.Description)}|{c.Notes?.Count ?? 0}"));
        }

        [SeaOfDirac("DeleteJournal", new[] { "journalId" }, typeof(string), typeof(string))]
        public static async Task<string> Delete(string journalId)
        {
            CheckId(journalId);
            await NoteClass.DeleteJournal(journalId);
            await Unfavorite(journalId, ignoreMissing: true);
            return "deleted";
        }

        [SeaOfDirac("FavoriteJournal", new[] { "journalId" }, typeof(string), typeof(string))]
        public static async Task<string> Favorite(string journalId)
        {
            CheckId(journalId);
            await NoteClass.AddJournalToFavorites(journalId);
            return "favorited";
        }

        [SeaOfDirac("UnfavoriteJournal", new[] { "journalId" }, typeof(string), typeof(string))]
        public static async Task<string> UnfavoriteJournal(string journalId)
        {
            CheckId(journalId);
            await Unfavorite(journalId, ignoreMissing: false);
            return "removed";
        }

        /// <summary>Ids of the favorite journals that still exist, separated by ';'.</summary>
        [SeaOfDirac("ListFavoriteJournals", new string[0], typeof(string))]
        public static async Task<string> Favorites()
        {
            if (!File.Exists(Path.Combine(JournalDir, FavoritesFile + ".json"))) return "";
            return string.Join(';', await NoteClass.GetFavoriteJournalIdsAsync(onlyResolved: true));
        }

        // ---- helpers ----

        private static string IdOf(NoteTypes.Journal j) =>
            $"{j.Identity.Name} v{j.Identity.Version} by {j.Identity.Author}__ID {j.Identity.ThemeID}";

        private static async Task<NoteTypes.Journal> Load(string id)
        {
            if (!File.Exists(Path.Combine(JournalDir, id + ".json"))) throw new KeyNotFoundException($"No journal '{id}'.");
            return await NoteClass.GetJournal(id);
        }

        private static async Task Unfavorite(string id, bool ignoreMissing)
        {
            if (!File.Exists(Path.Combine(JournalDir, FavoritesFile + ".json")))
            {
                if (ignoreMissing) return;
                throw new KeyNotFoundException($"'{id}' is not a favorite.");
            }
            try { await NoteClass.RemoveJournalFromFavorites(id); }
            catch (Exception) when (ignoreMissing) { }
        }

        private static void CheckText(string text, string what)
        {
            if (string.IsNullOrWhiteSpace(text) || !SafeText.IsMatch(text) || text.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException($"The journal {what} may use letters, digits, spaces, '.', '_', ''' and '-' (up to 60 characters).");
        }

        private static void CheckId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !SafeId.IsMatch(id) || id.Contains("..", StringComparison.Ordinal) || id == FavoritesFile)
                throw new ArgumentException("That is not a journal id.");
        }
    }
}
