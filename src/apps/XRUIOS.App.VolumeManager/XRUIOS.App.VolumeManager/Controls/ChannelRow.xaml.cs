using System.Windows.Controls;

namespace XRUIOS.App.VolumeManager.Controls
{
    public partial class ChannelRow : UserControl
    {
        public ChannelRow() => InitializeComponent();

        public ChannelRow(string appName, int value) : this()
        {
            AppText.Text      = appName;
            LevelSlider.Value = value;
            ValueText.Text    = value.ToString();
        }

        private void LevelSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
            => ValueText.Text = ((int)e.NewValue).ToString();
    }
}
