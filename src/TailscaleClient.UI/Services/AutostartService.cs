using System.Runtime.Versioning;
using System.Text;

namespace TailscaleClient.UI.Services;

/// <summary>
/// Cross-platform "launch at user login" toggle.
///
/// Windows  : writes the exe path to HKCU\...\Run.
/// macOS    : drops a LaunchAgent plist under ~/Library/LaunchAgents.
/// Linux    : XDG autostart .desktop file under ~/.config/autostart.
///
/// User-scoped only — no admin / sudo required, and no system-wide changes.
/// </summary>
public interface IAutostartService
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

public static class AutostartService
{
    public const string AppId = "TailscaleClient";

    public static IAutostartService Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsAutostart();
        if (OperatingSystem.IsMacOS()) return new MacAutostart();
        if (OperatingSystem.IsLinux()) return new LinuxAutostart();
        return new NoopAutostart();
    }

    /// <summary>Best-effort path to the running executable for autostart registration.
    /// Single-file publish gives the .exe directly; dev builds give the host dll.</summary>
    internal static string ExePath() =>
        Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
}

internal sealed class NoopAutostart : IAutostartService
{
    public bool IsEnabled => false;
    public void Enable() { }
    public void Disable() { }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsAutostart : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AutostartService.AppId) is not null;
        }
    }

    public void Enable()
    {
        var exe = AutostartService.ExePath();
        if (string.IsNullOrEmpty(exe)) return;
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(AutostartService.AppId, $"\"{exe}\"");
    }

    public void Disable()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AutostartService.AppId, throwOnMissingValue: false);
    }
}

[SupportedOSPlatform("macos")]
internal sealed class MacAutostart : IAutostartService
{
    // launchd uses reverse-DNS labels; this also becomes the .plist filename.
    private const string Label = "com.tailscaleclient.autostart";

    private static string PlistPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "LaunchAgents", $"{Label}.plist");
        }
    }

    public bool IsEnabled => File.Exists(PlistPath);

    public void Enable()
    {
        var exe = AutostartService.ExePath();
        if (string.IsNullOrEmpty(exe)) return;
        var dir = Path.GetDirectoryName(PlistPath)!;
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        sb.AppendLine("<plist version=\"1.0\"><dict>");
        sb.AppendLine($"  <key>Label</key><string>{Label}</string>");
        sb.AppendLine("  <key>ProgramArguments</key><array>");
        sb.AppendLine($"    <string>{System.Security.SecurityElement.Escape(exe)}</string>");
        sb.AppendLine("  </array>");
        sb.AppendLine("  <key>RunAtLoad</key><true/>");
        sb.AppendLine("</dict></plist>");
        File.WriteAllText(PlistPath, sb.ToString());

        // Loaded automatically on next login. Best-effort hot-load now so the
        // user can quit + relaunch immediately and have it take effect.
        TryRun("launchctl", $"load \"{PlistPath}\"");
    }

    public void Disable()
    {
        if (File.Exists(PlistPath))
        {
            TryRun("launchctl", $"unload \"{PlistPath}\"");
            try { File.Delete(PlistPath); } catch (IOException) { }
        }
    }

    private static void TryRun(string fileName, string args)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName, args)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(2000);
        }
        catch { /* best-effort */ }
    }
}

[SupportedOSPlatform("linux")]
internal sealed class LinuxAutostart : IAutostartService
{
    private static string DesktopPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "autostart", "tailscaleclient.desktop");
        }
    }

    public bool IsEnabled => File.Exists(DesktopPath);

    public void Enable()
    {
        var exe = AutostartService.ExePath();
        if (string.IsNullOrEmpty(exe)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopPath)!);
        var content =
            "[Desktop Entry]\n" +
            "Type=Application\n" +
            "Name=Tailscale\n" +
            $"Exec={exe}\n" +
            "Terminal=false\n" +
            "X-GNOME-Autostart-enabled=true\n";
        File.WriteAllText(DesktopPath, content);
    }

    public void Disable()
    {
        try { if (File.Exists(DesktopPath)) File.Delete(DesktopPath); }
        catch (IOException) { }
    }
}
