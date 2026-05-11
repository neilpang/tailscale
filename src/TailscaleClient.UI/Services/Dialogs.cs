using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace TailscaleClient.UI.Services;

/// <summary>Cross-platform message dialogs + clipboard helper.</summary>
public static class Dialogs
{
    // Shared title-bar icon for all message-box dialogs. Lazy so we never
    // touch Avalonia/Skia before the app initializes.
    private static WindowIcon? _icon;
    private static WindowIcon Icon =>
        _icon ??= IconFactory.CreateDotIcon(Color.FromRgb(0x10, 0xB9, 0x81), size: 64);

    /// <summary>Resolves the active main window (or null in tests / headless mode).</summary>
    public static TopLevel? MainTopLevel() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public static async Task ShowAsync(TopLevel? owner, string title, string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = message,
            ButtonDefinitions = ButtonEnum.Ok,
            Icon = MsBox.Avalonia.Enums.Icon.Info,
            WindowIcon = Icon,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        });
        if (owner is Window w) await box.ShowWindowDialogAsync(w);
        else await box.ShowAsync();
    }

    public static async Task<bool> ConfirmAsync(TopLevel? owner, string title, string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = message,
            ButtonDefinitions = ButtonEnum.YesNo,
            Icon = MsBox.Avalonia.Enums.Icon.Question,
            WindowIcon = Icon,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        });
        var result = owner is Window w
            ? await box.ShowWindowDialogAsync(w)
            : await box.ShowAsync();
        return result == ButtonResult.Yes;
    }

    public static async Task CopyAsync(TopLevel? owner, string? text)
    {
        if (string.IsNullOrEmpty(text) || owner is null) return;
        var clip = owner.Clipboard;
        if (clip is null) return;
        await clip.SetTextAsync(text);
        Toast.Copied(text);
    }
}
