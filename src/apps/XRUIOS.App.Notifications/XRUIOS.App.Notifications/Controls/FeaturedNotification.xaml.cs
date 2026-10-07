using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Notifications.Controls
{
    public partial class FeaturedNotification : UserControl
    {
        public FeaturedNotification() => InitializeComponent();

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(FeaturedNotification),
                new PropertyMetadata("", (d, e) => ((FeaturedNotification)d).TitleText.Text = (string)e.NewValue));

        public string Body
        {
            get => (string)GetValue(BodyProperty);
            set => SetValue(BodyProperty, value);
        }
        public static readonly DependencyProperty BodyProperty =
            DependencyProperty.Register(nameof(Body), typeof(string), typeof(FeaturedNotification),
                new PropertyMetadata("", (d, e) => ((FeaturedNotification)d).BodyText.Text = (string)e.NewValue));
    }
}
