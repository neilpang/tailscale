using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TailscaleClient.Core.Models;

namespace TailscaleClient.UI.Converters;

public sealed class BackendStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            BackendState.Running => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
            BackendState.Starting => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            BackendState.NeedsLogin or BackendState.NeedsMachineAuth =>
                new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            BackendState.Stopped => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
            _ => new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
        };
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BackendStateToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            BackendState.Running => "Connected",
            BackendState.Starting => "Connecting…",
            BackendState.NeedsLogin => "Sign in required",
            BackendState.NeedsMachineAuth => "Machine auth required",
            BackendState.Stopped => "Disconnected",
            BackendState.NoState => "Initializing…",
            null => "Unknown",
            _ => value,
        };
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BooleanInverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

public sealed class IpListConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable ips)
        {
            var parts = new List<string>();
            foreach (var ip in ips) parts.Add(ip?.ToString() ?? "");
            return string.Join(", ", parts);
        }
        return "";
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class FirstOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable items)
            foreach (var item in items) return item?.ToString() ?? "";
        return "";
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class RelativeTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto || dto == default) return "—";
        var delta = DateTimeOffset.UtcNow - dto;
        if (delta.TotalSeconds < 60) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 30) return $"{(int)delta.TotalDays}d ago";
        return dto.LocalDateTime.ToString("yyyy-MM-dd");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BytesToHumanConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes) return "0 B";
        if (bytes == 0) return "0 B";
        var unit = 0;
        double v = bytes;
        while (v >= 1024 && unit < Units.Length - 1) { v /= 1024; unit++; }
        return $"{v:0.##} {Units[unit]}";
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class OnlineToBrushConverter : IValueConverter
{
    private static readonly IBrush Online = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly IBrush Offline = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Online : Offline;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Renders a peer's last-seen state as a single string. If <c>Online</c>, returns
/// "online"; otherwise formats the <c>LastSeen</c> timestamp. Bound at peer-row
/// scope, so the parameter is the <see cref="PeerStatus"/> itself.
/// </summary>
public sealed class OnlineToLastSeenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PeerStatus p) return "—";
        if (p.Online) return "online";
        if (p.LastSeen == default) return "—";
        var delta = DateTimeOffset.UtcNow - p.LastSeen;
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 30) return $"{(int)delta.TotalDays}d ago";
        return p.LastSeen.LocalDateTime.ToString("yyyy-MM-dd");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
