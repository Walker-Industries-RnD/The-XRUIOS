using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class ClipPanel : UserControl
    {
        public ClipPanel() => InitializeComponent();

        // Simple CLR properties so the parent XAML can set them directly.
        public string Title
        {
            get => TitleTextBlock.Text;
            set => TitleTextBlock.Text = value;
        }
        public string CornerNote
        {
            get => CornerTextBlock.Text;
            set => CornerTextBlock.Text = value;
        }
        public bool AccentBar
        {
            get => AccentStripe.Visibility == Visibility.Visible;
            set => AccentStripe.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public void AddCard(UIElement card) => ListHost.Children.Add(card);

        /// <summary>Add one cover tile to a 4-col grid; auto-adds rows.</summary>
        public void AddCover(UIElement tile)
        {
            const int Cols = 4;
            if (CoverHost.ColumnDefinitions.Count == 0)
            {
                CoverHost.Visibility = Visibility.Visible;
                ListHost.Visibility = Visibility.Collapsed;
                for (var i = 0; i < Cols; i++)
                    CoverHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            var idx = CoverHost.Children.Count;
            var row = idx / Cols;
            var col = idx % Cols;
            while (CoverHost.RowDefinitions.Count <= row)
                CoverHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, col);
            CoverHost.Children.Add(tile);
        }
    }
}
