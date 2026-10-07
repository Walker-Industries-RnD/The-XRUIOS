using System;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>
/// Hole: live events from the XRUIOS instead of polling. Every app is granted ThemeChanged; the rest
/// follow each app's grants (Events.&lt;Name&gt; in the Manager). Keep the returned stream for as long as
/// the page wants updates and dispose it when the page goes away.
/// <code>
/// _themes = EventFeed.OnThemeChanged(t =&gt; Dispatcher.BeginInvoke(() =&gt; ApplyTheme(t)));
/// </code>
/// Callbacks arrive on a background thread - marshal to the UI thread before touching controls.
/// </summary>
public static class EventFeed
{
    /// <summary>Every event this app may receive.</summary>
    public static XruiosEventStream Subscribe(Action<XruiosEvent> onEvent, Action<Exception>? onError = null)
        => AppShell.Broker.Subscribe(onEvent, onError);

    /// <summary>Only events named <paramref name="eventName"/>, with the payload decoded.</summary>
    public static XruiosEventStream On<T>(string eventName, Action<T> handler, Action<Exception>? onError = null)
        => AppShell.Broker.Subscribe(e =>
        {
            if (e.Name == eventName) handler(EventJson.Read<T>(e.Payload));
        }, onError);

    public static XruiosEventStream OnThemeChanged(Action<ThemeInfo> handler) => On(XruiosEventNames.ThemeChanged, handler);
    public static XruiosEventStream OnNotificationPosted(Action<NotificationInfo> handler) => On(XruiosEventNames.NotificationPosted, handler);
    public static XruiosEventStream OnVolumeChanged(Action<VolumeChange> handler) => On(XruiosEventNames.VolumeChanged, handler);
    public static XruiosEventStream OnAlarmFired(Action<AlarmInfo> handler) => On(XruiosEventNames.AlarmFired, handler);
    public static XruiosEventStream OnWeatherUpdated(Action<WeatherSnapshot> handler) => On(XruiosEventNames.WeatherUpdated, handler);
    public static XruiosEventStream OnWorldEventAdded(Action<WorldEventInfo> handler) => On(XruiosEventNames.WorldEventAdded, handler);
}
