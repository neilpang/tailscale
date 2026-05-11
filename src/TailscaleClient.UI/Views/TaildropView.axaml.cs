using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TailscaleClient.Core.LocalApi;
using TailscaleClient.UI.Services;
using TailscaleClient.UI.ViewModels;

namespace TailscaleClient.UI.Views;

public partial class TaildropView : UserControl
{
    public TaildropView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private TailscaleService? Svc => (DataContext as MainViewModel)?.Service;

    private async Task RefreshAsync()
    {
        if (Svc is null) return;
        try
        {
            var files = await Svc.ListTaildropFilesAsync();
            ReceivedList.ItemsSource = files;
        }
        catch (Exception ex)
        {
            await Dialogs.ShowAsync(TopLevel.GetTopLevel(this), "Taildrop",
                $"Could not list Taildrop files: {ex.Message}");
        }
    }

    private async void RefreshReceived_Click(object? sender, RoutedEventArgs e) => await RefreshAsync();

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is null || sender is not Button { Tag: TaildropFile f }) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var picker = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = f.Name,
            Title = "Save received file",
        });
        if (picker is null) return;
        try
        {
            await using var src = await Svc.DownloadTaildropFileAsync(f.Name);
            await using var dst = await picker.OpenWriteAsync();
            await src.CopyToAsync(dst);
            await Svc.DeleteTaildropFileAsync(f.Name);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await Dialogs.ShowAsync(top, "Taildrop", $"Save failed: {ex.Message}");
        }
    }

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is null || sender is not Button { Tag: TaildropFile f }) return;
        try
        {
            await Svc.DeleteTaildropFileAsync(f.Name);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await Dialogs.ShowAsync(TopLevel.GetTopLevel(this), "Taildrop", $"Delete failed: {ex.Message}");
        }
    }
}
