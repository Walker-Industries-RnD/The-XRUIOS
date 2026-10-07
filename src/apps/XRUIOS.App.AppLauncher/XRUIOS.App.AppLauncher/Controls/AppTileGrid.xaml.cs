using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.AppLauncher.Controls
{
    public partial class AppTileGrid : UserControl
    {
        public int Columns { get; set; } = 5;

        public AppTileGrid() => InitializeComponent();

        public void Clear()
        {
            TileHost.Children.Clear();
            TileHost.RowDefinitions.Clear();
            TileHost.ColumnDefinitions.Clear();
        }

        public void Add(UIElement tile)
        {
            if (TileHost.ColumnDefinitions.Count == 0)
                for (var i = 0; i < Columns; i++)
                    TileHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var idx = TileHost.Children.Count;
            var row = idx / Columns;
            var col = idx % Columns;
            while (TileHost.RowDefinitions.Count <= row)
                TileHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, col);
            TileHost.Children.Add(tile);
        }
    }
}
