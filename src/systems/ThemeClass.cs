using Microsoft.Maui.Storage;
using System.Text.Json.Nodes;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.ThemeSystem;
using static XRUIOS.Barebones.XRUIOS;


namespace XRUIOS.Barebones.Functions
{
    public static class ThemeSystem
    {


        public static ObservableProperty<XRUIOSTheme> CurrentTheme { get; private set; }
        //Keep in mind a XRUIOS theme might completely ignore the values from the themes or ignore them partly


        //C
        //Remember to create a folder with the same name in the directory containing all the assets!

        public static async Task SaveTheme(XRUIOSTheme theme)
        {
            var directoryPath = Path.Combine(DataPath, "Themes");
            var fileName = $"{theme.Identity.Name} v{theme.Identity.Version} by {theme.Identity.Author}__ID {theme.Identity.ThemeID}";

            var filePath = Path.Combine(DataPath, "Themes", fileName);

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException("This theme already exists; please change the name.");
            }

            await JSONDataHandler.CreateJsonFile(fileName, directoryPath, new JsonObject());

            var json = await JSONDataHandler.LoadJsonFile(fileName, directoryPath);
            json = await JSONDataHandler.AddToJson<XRUIOSTheme>(json, "Data", theme, encryptionKey);
            await JSONDataHandler.SaveJson(json);
        }

        //R
        public static async Task<List<XRUIOSTheme>> GetAllXRUIOSThemes()
        {
            List<XRUIOSTheme> Themes = new List<XRUIOSTheme>();

            var directoryPath = Path.Combine(DataPath, "Themes");

            var themePaths = Directory.EnumerateFiles(directoryPath);

            foreach (var item in themePaths)
            {
                var json = await JSONDataHandler.LoadJsonFile((Path.GetFileNameWithoutExtension(item)), directoryPath);

                var themeFile = (XRUIOSTheme)await JSONDataHandler.GetVariable<XRUIOSTheme>(json, "Data", encryptionKey);

                Themes.Add(themeFile);

            }

            return Themes;
        }

        public static async Task<XRUIOSTheme> GetXRUIOSTheme(string FileName)
        {

            var directoryPath = Path.Combine(DataPath, "Themes");

            var json = await JSONDataHandler.LoadJsonFile(FileName, directoryPath);

            var themeFile = (XRUIOSTheme)await JSONDataHandler.GetVariable<XRUIOSTheme>(json, "Data", encryptionKey);

            return themeFile;
        }

        public static async Task<XRUIOSTheme> GetCurrentTheme(string FileName)
        {
            return CurrentTheme;
        }

        //U

        //Remember to put Identity.Version up
        public static async Task UpdateTheme(XRUIOSTheme theme, XRUIOSTheme newTheme)
        {
            var directoryPath = Path.Combine(DataPath, "Themes");
            var fileName = $"{theme.Identity.Name} v{theme.Identity.Version} by {theme.Identity.Author}__ID {theme.Identity.ThemeID}";

            var filePath = Path.Combine(DataPath, "Themes", fileName);

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException("This theme does not exist.");
            }

            var json = await JSONDataHandler.LoadJsonFile(fileName, directoryPath);
            json = await JSONDataHandler.UpdateJson<XRUIOSTheme>(json, "Data", newTheme, encryptionKey);
            await JSONDataHandler.SaveJson(json);
        }

        public static async Task SetTheme(string FileName)
        {
            CurrentTheme.Set(await GetXRUIOSTheme(FileName));

        }


        //D
        public static async Task DeleteXRUIOSTheme(string FileName)
        {
            var directoryPath = Path.Combine(DataPath, "Themes");
            var filePath = Path.Combine(DataPath, "Themes", FileName);

            File.Delete(filePath);
        }


    }

}
