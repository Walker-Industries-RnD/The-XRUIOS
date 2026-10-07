using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Power.Controls
{
    public partial class BatteryChip : UserControl
    {
        public BatteryChip() => InitializeComponent();

        public int Percent
        {
            get => (int)GetValue(PercentProperty);
            set => SetValue(PercentProperty, value);
        }
        public static readonly DependencyProperty PercentProperty =
            DependencyProperty.Register(nameof(Percent), typeof(int), typeof(BatteryChip),
                new PropertyMetadata(0, (d, e) => ((BatteryChip)d).PercentText.Text = "%" + e.NewValue));
    }
}
