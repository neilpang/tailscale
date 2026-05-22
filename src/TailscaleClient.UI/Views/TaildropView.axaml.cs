using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TailscaleClient.Core.LocalApi;
using TailscaleClient.UI.Services;
using TailscaleClient.UI.ViewModels;

namespace TailscaleClient.UI.Views;

public partial class TaildropView : UserControl
{
    private UserSettings? _settings;
    private TailscaleService? _wiredSvc;

    public TaildropView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private TailscaleService? Svc => (DataContext as MainViewModel)?.Service;

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _settings = App.Services.GetRequiredService<UserSettings>();
        _settings.TaildropAutoSaveFolderChanged += RefreshAutoSavePath;
        RefreshAutoSavePath();

        _wiredSvc = Svc;
        if (_wiredSvc is not null)
            _wiredSvc.TaildropChanged += OnTaildropChanged;

        await RefreshAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_settings is not null)
            _settings.TaildropAutoSaveFolderChanged -= RefreshAutoSavePath;
        if (_wiredSvc is not null)
            _wiredSvc.TaildropChanged -= OnTaildropChanged;
        _settings = null;
        _wiredSvc = null;
    }

    private void OnTaildropChanged() =>
        Dispatcher.UIThread.Post(async () => await RefreshAsync());

    private void RefreshAutoSavePath()
    {
        var folder = _settings?.TaildropAutoSaveFolder;
        AutoSavePath.Text = string.IsNullOrEmpty(folder)
            ? "(not set — save manually)"
            : folder;
    }

    private async void BrowseAutoSave_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || _settings is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Auto-save received Taildrop files to",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;
        _settings.TaildropAutoSaveFolder = path;
    }

    private void ClearAutoSave_Click(object? sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        _settings.TaildropAutoSaveFolder = null;
    }

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
