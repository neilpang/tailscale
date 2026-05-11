using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace TailscaleClient.UI.Services;

/// <summary>
/// App-wide toast notifications. Uses Avalonia's built-in
/// <see cref="WindowNotificationManager"/>, parented to the main window so the
/// bubble fades in the lower-right corner regardless of which view triggered
/// the action.
/// </summary>
public static class Toast
{
    private static WindowNotificationManager? _mgr;

    public static void Initialize(TopLevel topLevel)
    {
        _mgr = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3,
            Margin = new Avalonia.Thickness(0, 0, 12, 12),
        };
    }

    public static void Show(string title, string message,
        NotificationType type = NotificationType.Information, TimeSpan? duration = null)
    {
        _mgr?.Show(new Notification(title, message, type, duration ?? TimeSpan.FromSeconds(2)));
    }

    /// <summary>Quick success toast for "X copied to clipboard".</summary>
    public static void Copied(string what)
    {
        // Truncate very long values so the toast stays a sensible size.
        var shown = what.Length > 80 ? what[..77] + "…" : what;
        Show("Copied to clipboard", shown, NotificationType.Success);
    }
}
