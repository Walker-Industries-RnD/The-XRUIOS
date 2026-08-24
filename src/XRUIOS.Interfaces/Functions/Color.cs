using YuukoProtocol;


namespace XRUIOS.Barebones.Interfaces
{
    public class Color
    {
        public int R = 0;
        public int G = 0;
        public int B = 0;
        public int A = 0;

        public Color() { }

        public Color(int r, int g, int b, int a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        public static readonly Color Red = new Color(255, 0, 0, 255);
        public static readonly Color Green = new Color(0, 255, 0, 255);
        public static readonly Color Blue = new Color(0, 0, 255, 255);
        public static readonly Color White = new Color(255, 255, 255, 255);
        public static readonly Color Black = new Color(0, 0, 0, 255);
        public static readonly Color Yellow = new Color(255, 255, 0, 255);
        public static readonly Color Cyan = new Color(0, 255, 255, 255);
        public static readonly Color Magenta = new Color(255, 0, 255, 255);
        public static readonly Color Gray = new Color(128, 128, 128, 255);
        public static readonly Color DarkGray = new Color(64, 64, 64, 255);
        public static readonly Color LightGray = new Color(192, 192, 192, 255);
        public static readonly Color Orange = new Color(255, 165, 0, 255);
        public static readonly Color Pink = new Color(255, 192, 203, 255);
        public static readonly Color Purple = new Color(128, 0, 128, 255);
        public static readonly Color Brown = new Color(139, 69, 19, 255);
        public static readonly Color Maroon = new Color(128, 0, 0, 255);
        public static readonly Color Olive = new Color(128, 128, 0, 255);
        public static readonly Color Navy = new Color(0, 0, 128, 255);
        public static readonly Color Teal = new Color(0, 128, 128, 255);
        public static readonly Color Lime = new Color(0, 255, 0, 255);
        public static readonly Color Gold = new Color(255, 215, 0, 255);
        public static readonly Color Silver = new Color(192, 192, 192, 255);
        public static readonly Color Coral = new Color(255, 127, 80, 255);
        public static readonly Color Salmon = new Color(250, 128, 114, 255);
        public static readonly Color Indigo = new Color(75, 0, 130, 255);
        public static readonly Color Violet = new Color(238, 130, 238, 255);
        public static readonly Color SkyBlue = new Color(135, 206, 235, 255);
        public static readonly Color DeepSkyBlue = new Color(0, 191, 255, 255);
        public static readonly Color Transparent = new Color(0, 0, 0, 0);

        public static readonly Color CyberpunkYellow = new Color(255, 230, 0, 255);    // Neon yellow
        public static readonly Color JohnnySilver = new Color(192, 192, 192, 255);  // Metallic silver
        public static readonly Color VergilBlue = new Color(50, 90, 255, 255);    // Devil May Cry Vergil blue
        public static readonly Color GyroGreen = new Color(0, 200, 120, 255);    // JoJo Gyro green
        public static readonly Color ValentinePurple = new Color(150, 0, 200, 255);    // Rell Valentine purple
        public static readonly Color BondrewdBlack = new Color(20, 20, 30, 255);     // Deep black w/ blue tint
        public static readonly Color KingCrimson = new Color(220, 20, 60, 255);    // Crimson red
        public static readonly Color MayanoOrange = new Color(255, 140, 0, 255);    // Bright orange
        public static readonly Color KillerQueenPink = new Color(255, 105, 180, 255);  // Hot pink
        public static readonly Color RiasRed = new Color(200, 0, 50, 255);     // Rias Gremory red
        public static readonly Color GordonOrange = new Color(255, 100, 0, 255);    // Gordon Freeman orange
        public static readonly Color ZaWaurdoYellow = new Color(255, 255, 50, 255);   // Bright yellow
        public static readonly Color ChiefGreen = new Color(0, 100, 0, 255);      // Master Chief armor green
        public static readonly Color UndyneBlue = new Color(0, 90, 255, 255);     // Strong blue
        public static readonly Color SusiePurple = new Color(160, 50, 180, 255);   // Deep purple

        public static readonly Color KazumaBrown = new Color(139, 69, 19, 255);    // Dark brown
        public static readonly Color BatmanBlack = new Color(10, 10, 10, 255);     // Jet black
        public static readonly Color SaberYellow = new Color(255, 215, 0, 255);    // Golden yellow
        public static readonly Color ShiroOrange = new Color(255, 120, 40, 255);   // Warm orange
        public static readonly Color GokuBlack = new Color(60, 0, 60, 255);      // Dark purple-black

        public static readonly Color MakishimaWhite = new Color(240, 240, 240, 255);  // Soft white
        public static readonly Color UltraInstinctWhite = new Color(255, 255, 255, 255);  // Bright white
        public static readonly Color TheDrinkPurple = new Color(128, 0, 255, 255);    // Neon purple


    }

}
