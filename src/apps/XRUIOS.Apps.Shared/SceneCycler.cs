using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.Apps.Shared
{
    /// <summary>
    /// TEMPORARY dev helper. Apps register named "scenes" (a label + a factory
    /// that produces the root visual for it), and any page can drop a
    /// <see cref="SceneSwitchButton"/> in its top corner to cycle through them.
    /// Meant to be pulled out once the app has proper navigation - the code
    /// isn't referenced from anywhere else, so deleting these two files
    /// removes the whole feature.
    /// </summary>
    public static class SceneCycler
    {
        public sealed record Scene(string Label, Func<UIElement> Factory);

        private static readonly List<Scene> _scenes = new();
        private static int _index;

        /// <summary>Called from App.xaml.cs to declare the app's scenes.</summary>
        public static void Register(string label, Func<UIElement> factory)
            => _scenes.Add(new Scene(label, factory));

        public static string CurrentLabel =>
            _scenes.Count == 0 ? "" : _scenes[_index].Label;

        public static string NextLabel =>
            _scenes.Count < 2 ? "" : _scenes[(_index + 1) % _scenes.Count].Label;

        /// <summary>Advance to the next scene and swap the Application root visual.</summary>
        public static void Next()
        {
            if (_scenes.Count < 2) return;
            _index = (_index + 1) % _scenes.Count;
            if (Application.Current is not null)
                Application.Current.RootVisual = _scenes[_index].Factory();
        }

        /// <summary>Initial root visual - the first registered scene.</summary>
        public static UIElement? Initial() =>
            _scenes.Count == 0 ? null : _scenes[0].Factory();
    }

    /// <summary>
    /// Floating pill that shows "Next: &lt;label&gt;" and cycles SceneCycler on
    /// click. Drop it into any page's root Grid with no other layout - it
    /// pins itself to the top-right corner.
    /// </summary>
    public sealed class SceneSwitchButton : Button
    {
        public SceneSwitchButton()
        {
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(0, 12, 12, 0);
            Padding = new Thickness(14, 6, 14, 6);
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x00, 0x00, 0x00));
            Foreground = new SolidColorBrush(Colors.White);
            BorderThickness = new Thickness(0);
            FontSize = 12;
            Refresh();
            Click += (_, _) => { SceneCycler.Next(); Refresh(); };
        }

        private void Refresh()
        {
            var next = SceneCycler.NextLabel;
            Content = string.IsNullOrEmpty(next)
                ? SceneCycler.CurrentLabel
                : "Switch → " + next;
        }
    }
}
