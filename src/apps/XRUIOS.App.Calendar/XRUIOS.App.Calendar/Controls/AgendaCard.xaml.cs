using System.Windows.Controls;

namespace XRUIOS.App.Calendar.Controls
{
    public partial class AgendaCard : UserControl
    {
        public AgendaCard() => InitializeComponent();

        public AgendaCard(string chip, string title, string time) : this()
        {
            ChipText.Text  = chip;
            TitleText.Text = title;
            TimeText.Text  = time;
        }
    }
}
