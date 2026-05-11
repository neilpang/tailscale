using System.IO;
using System.Diagnostics;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TailscaleClient.Core.Models;
using TailscaleClient.UI.Services;

namespace TailscaleClient.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public TailscaleService Service { get; }

    public MainViewModel(TailscaleService service)
    {
        Service = service;
    }

    [RelayCommand] private Task Connect() => Service.ConnectAsync();
    [RelayCommand] private Task Disconnect() => Service.DisconnectAsync();
    [RelayCommand] private Task Login() => Service.LoginAsync();
    [RelayCommand] private Task Logout() => Service.LogoutAsync();
    [RelayCommand] private Task Refresh() => Service.RefreshAsync();

    [RelayCommand]
    private void OpenAuthUrl()
    {
        var url = Service.AuthUrl;
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _ = Dialogs.ShowAsync(Dialogs.MainTopLevel(), "Tailscale", $"Could not open browser: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return;
        await Dialogs.CopyAsync(Dialogs.MainTopLevel(), ip);
    }

    [RelayCommand]
    private async Task CopyText(string? text) => await Dialogs.CopyAsync(Dialogs.MainTopLevel(), text);

    [RelayCommand]
    private async Task SetExitNode(PeerStatus? peer) => await Service.SetExitNodeAsync(peer?.ID);

    [RelayCommand]
    private async Task ClearExitNode() => await Service.SetExitNodeAsync(null);

    [RelayCommand]
    private async Task PingPeer(PeerStatus? peer)
    {
        if (peer is null || peer.TailscaleIPs.Count == 0) return;
        var ip = peer.TailscaleIPs[0];
        var result = await Service.PingAsync(ip);
        string msg;
        if (result is null)
            msg = string.IsNullOrEmpty(Service.LastError) ? "Ping failed." : $"Ping failed: {Service.LastError}";
        else if (!string.IsNullOrEmpty(result.Err))
            msg = $"Ping error: {result.Err}";
        else
        {
            var via = !string.IsNullOrEmpty(result.Endpoint) ? result.Endpoint
                    : !string.IsNullOrEmpty(result.DERPRegionCode) ? $"DERP {result.DERPRegionCode}"
                    : "direct";
            msg = $"{peer.DisplayName} via {via} in {result.LatencySeconds * 1000:0.#} ms";
        }
        await Dialogs.ShowAsync(Dialogs.MainTopLevel(), "Ping result", msg);
    }

    [RelayCommand]
    private async Task SendFileTo(PeerStatus? peer)
    {
        if (peer is null) return;
        var top = Dialogs.MainTopLevel();
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Send to {peer.DisplayName}",
            AllowMultiple = false,
        });
        if (files.Count == 0) return;
        var file = files[0];
        try
        {
            await using var fs = await file.OpenReadAsync();
            await Service.SendTaildropFileAsync(peer.ID, file.Name, fs, fs.CanSeek ? fs.Length : null);
            await Dialogs.ShowAsync(top, "Taildrop", $"Sent {file.Name} to {peer.DisplayName}.");
        }
        catch (Exception ex)
        {
            await Dialogs.ShowAsync(top, "Taildrop", $"Send failed: {ex.Message}");
        }
    }
}
