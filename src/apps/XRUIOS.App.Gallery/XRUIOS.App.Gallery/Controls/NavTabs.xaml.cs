using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Gallery.Controls
{
    public partial class NavTabs : UserControl
    {
        public NavTabs() => InitializeComponent();

        public string Selected
        {
            get => (string)GetValue(SelectedProperty);
            set => SetValue(SelectedProperty, value);
        }
        public static readonly DependencyProperty SelectedProperty =
            DependencyProperty.Register(nameof(Selected), typeof(string), typeof(NavTabs),
                new PropertyMetadata("Images", (d, e) => ((NavTabs)d).UpdateUnderlines((string)e.NewValue)));

        private void UpdateUnderlines(string sel)
        {
            AllUnderline.Visibility      = sel == "All"      ? Visibility.Visible : Visibility.Collapsed;
            AlbumsUnderline.Visibility   = sel == "Albums"   ? Visibility.Visible : Visibility.Collapsed;
            ImagesUnderline.Visibility   = sel == "Images"   ? Visibility.Visible : Visibility.Collapsed;
            SettingsUnderline.Visibility = sel == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            TrashUnderline.Visibility    = sel == "Trash"    ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
