using System.Windows.Controls;

namespace XRUIOS.App.Time.Controls
{
    public partial class StopwatchCard : UserControl
    {
        public StopwatchCard() => InitializeComponent();

        public StopwatchCard(string title, string lapTime, string lap) : this()
        {
            TitleText.Text   = title;
            LapTimeText.Text = lapTime;
            LapText.Text     = lap;
        }
    }
}
