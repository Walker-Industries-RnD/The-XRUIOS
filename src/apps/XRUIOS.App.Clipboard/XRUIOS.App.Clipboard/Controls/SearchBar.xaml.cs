using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class SearchBar : UserControl
    {
        public SearchBar() => InitializeComponent();

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SearchBar),
                new PropertyMetadata("", (d, e) => {
                    var self = (SearchBar)d;
                    // Show the placeholder as the initial text when the box is empty.
                    if (string.IsNullOrEmpty(self.Input.Text)) self.Input.Text = (string)e.NewValue;
                }));
    }
}
