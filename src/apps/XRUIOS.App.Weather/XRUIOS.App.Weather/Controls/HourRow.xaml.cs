using System.Windows.Controls;

namespace XRUIOS.App.Weather.Controls
{
    public partial class HourRow : UserControl
    {
        public HourRow() => InitializeComponent();

        public HourRow(string condition, string precip, string hour, string temp) : this()
        {
            ConditionText.Text = condition;
            PrecipText.Text    = precip;
            HourText.Text      = hour;
            TempText.Text      = temp;
        }
    }
}
