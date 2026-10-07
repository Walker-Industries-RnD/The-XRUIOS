using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    public static class NotificationClass
    {
        public struct NotificationContent
        {
            public string Action;
            public Dictionary<string, string> Args;
            public List<string> Texts;
            public FileRecord? InlineImage;
            public FileRecord? Logo;
            public List<Button> Buttons;
            public string? Tag;
            public string? Group;
            public DateTime? ExpirationTime;

            public NotificationContent() { }

            public NotificationContent(
                string action,
                Dictionary<string, string>? args = null,
                List<string>? texts = null,
                FileRecord? inlineImage = null,
                FileRecord? logo = null,
                List<Button>? buttons = null,
                string? tag = null,
                string? group = null,
                DateTime? expirationTime = null)
            {
                Action = action;
                Args = args ?? new Dictionary<string, string>();
                Texts = texts ?? new List<string>();
                InlineImage = inlineImage;
                Logo = logo;
                Buttons = buttons ?? new List<Button>();
                Tag = tag;
                Group = group;
                ExpirationTime = expirationTime;
            }

            public struct Button
            {
                public string Content;
                public string Action;
                public Dictionary<string, string> Args;
                public bool BackgroundActivation;

                public Button() { }

                public Button(string content, string action, Dictionary<string, string>? args = null, bool backgroundActivation = true)
                {
                    Content = content;
                    Action = action;
                    Args = args ?? new Dictionary<string, string>();
                    BackgroundActivation = backgroundActivation;
                }
            }
        }


    }
}
