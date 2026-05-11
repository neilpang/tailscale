using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace TailscaleClient.UI.Services;

/// <summary>Cross-platform message dialogs + clipboard helper.</summary>
public static class Dialogs
{
    /// <summary>Resolves the active main window (or null in tests / headless mode).</summary>
    public static TopLevel? MainTopLevel() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public static async Task ShowAsync(TopLevel? owner, string title, string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Info);
        if (owner is Window w) await box.ShowWindowDialogAsync(w);
        else await box.ShowAsync();
    }

    public static async Task<bool> ConfirmAsync(TopLevel? owner, string title, string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.YesNo, Icon.Question);
        var result = owner is Window w
            ? await box.ShowWindowDialogAsync(w)
            : await box.ShowAsync();
        return result == ButtonResult.Yes;
    }

    public static async Task CopyAsync(TopLevel? owner, string? text)
    {
        if (string.IsNullOrEmpty(text) || owner is null) return;
        var clip = owner.Clipboard;
        if (clip is not null) await clip.SetTextAsync(text);
    }
}
