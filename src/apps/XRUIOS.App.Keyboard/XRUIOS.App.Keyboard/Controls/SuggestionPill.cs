using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Keyboard.Controls
{
    /// <summary>Thin-outlined word chip in the suggestion strip.</summary>
    public sealed class SuggestionPill : Button
    {
        public string Word
        {
            get => (string)GetValue(WordProperty);
            set => SetValue(WordProperty, value);
        }
        public static readonly DependencyProperty WordProperty =
            DependencyProperty.Register(nameof(Word), typeof(string), typeof(SuggestionPill),
                new PropertyMetadata("", (d, e) => ((SuggestionPill)d).Content = e.NewValue));

        public SuggestionPill()
        {
            Background = new SolidColorBrush(Colors.White);
            Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            BorderThickness = new Thickness(1);
            Padding = new Thickness(14, 8, 14, 8);
            FontSize = 16;
            Margin = new Thickness(6, 0, 6, 0);
            HorizontalContentAlignment = HorizontalAlignment.Center;
        }
    }
}
