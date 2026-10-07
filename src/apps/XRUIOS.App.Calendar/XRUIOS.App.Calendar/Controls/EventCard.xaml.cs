using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Calendar.Controls
{
    public partial class EventCard : UserControl
    {
        public bool IsSelected { get; }

        public EventCard() => InitializeComponent();

        public EventCard(string title, string startEnd, string description,
                         string importance, string repeats, bool isSelected = false) : this()
        {
            IsSelected = isSelected;

            TitleText.Text      = title;
            TimeText.Text       = startEnd;
            DescText.Text       = description;
            ImportanceText.Text = importance;
            RepeatsText.Text    = repeats;

            // Default XAML draws the unselected variant; swap tokens for selected.
            if (isSelected)
            {
                var white = new SolidColorBrush(Colors.White);
                var subInk = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

                Host.Background        = new SolidColorBrush(Color.FromRgb(0x3F, 0x8A, 0xD1));
                Stripe.Background      = white;
                IconText.Foreground    = white;
                TitleText.Foreground   = white;
                TimeText.Foreground    = subInk;
                DescText.Foreground    = subInk;
                ImportanceText.Foreground = white;
                RepeatsText.Foreground = subInk;
            }
            else
            {
                var ink    = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
                var subInk = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
                IconText.Foreground    = ink;
                TitleText.Foreground   = ink;
                TimeText.Foreground    = subInk;
                DescText.Foreground    = subInk;
                RepeatsText.Foreground = subInk;
            }
        }
    }
}
