using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using TailscaleClient.Core.LocalApi;
using TailscaleClient.Core.Models;

namespace TailscaleClient.UI.Services;

/// <summary>
/// Singleton wrapper over <see cref="LocalApiClient"/>. Polls status, keeps a
/// background IPN bus connection, and raises <see cref="PropertyChanged"/>
/// for the UI. All mutating methods funnel through <see cref="LocalApiClient"/>
/// and refresh state on success.
/// </summary>
public sealed class TailscaleService : INotifyPropertyChanged, IDisposable
{
    private readonly LocalApiClient _api;
    private readonly CancellationTokenSource _cts = new();
    private Task? _busTask;
    private Task? _pollTask;

    private Status? _status;
    private Prefs? _prefs;
    private string? _authUrl;
    private string? _lastError;
    private bool _isConnecting;

    public TailscaleService() : this(new LocalApiClient()) { }

    public TailscaleService(LocalApiClient api)
    {
        _api = api;
    }

    // ─────────────────── Observable state ───────────────────

    public Status? Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(BackendState));
            OnPropertyChanged(nameof(IsRunning)); OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(CanSignIn)); OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
            OnPropertyChanged(nameof(Self));
            OnPropertyChanged(nameof(ExitNodeCandidates)); OnPropertyChanged(nameof(CurrentExitNodeName));
            OnPropertyChanged(nameof(TailnetName));
            RefreshSortedPeers(); }
    }

    public Prefs? Prefs
    {
        get => _prefs;
        private set { _prefs = value; OnPropertyChanged();
            OnPropertyChanged(nameof(WantRunning)); OnPropertyChanged(nameof(AcceptRoutes));
            OnPropertyChanged(nameof(AcceptDns)); OnPropertyChanged(nameof(ShieldsUp));
            OnPropertyChanged(nameof(ExitNodeId)); OnPropertyChanged(nameof(ExitNodeAllowLan));
            OnPropertyChanged(nameof(RunSsh)); OnPropertyChanged(nameof(CurrentExitNodeName));
            OnPropertyChanged(nameof(AdvertisedRoutes)); }
    }

    public string? AuthUrl
    {
        get => _authUrl;
        private set { _authUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAuthUrl)); }
    }

    public string? LastError
    {
        get => _lastError;
        private set { _lastError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        private set { _isConnecting = value; OnPropertyChanged(); }
    }

    // Derived helpers ----------------------------------------

    public string BackendState => _status?.BackendState ?? "Unknown";
    public bool IsRunning => BackendState == Core.Models.BackendState.Running;
    public bool IsLoggedIn => _status is not null && BackendState != Core.Models.BackendState.NeedsLogin
                                                  && BackendState != Core.Models.BackendState.NoState;
    public bool CanSignIn => !IsLoggedIn;
    public bool CanConnect => IsLoggedIn && !IsRunning;
    public bool CanDisconnect => IsRunning;
    public bool HasAuthUrl => !string.IsNullOrEmpty(_authUrl);
    public bool HasError => !string.IsNullOrEmpty(_lastError);
    public PeerStatus? Self => _status?.Self;
    public string TailnetName => _status?.CurrentTailnet?.Name ?? "";

    /// <summary>
    /// Stable, observable view of the tailnet sorted online-first by display name.
    /// We never replace the collection — only diff and merge in place — so UI
    /// controls' selection survives the 5 s status poll.
    /// </summary>
    public ObservableCollection<PeerStatus> SortedPeers { get; } = new();

    private void RefreshSortedPeers()
    {
        // Compose the desired list off the UI thread; apply mutations on it.
        List<PeerStatus> fresh;
        if (_status?.Peer is null)
        {
            fresh = new List<PeerStatus>();
        }
        else
        {
            fresh = _status.Peer.Values
                .OrderByDescending(p => p.Online)
                .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        void Apply()
        {
            // Drop peers no longer present (IEquatable matches by StableNodeID).
            for (int i = SortedPeers.Count - 1; i >= 0; i--)
            {
                if (!fresh.Contains(SortedPeers[i]))
                    SortedPeers.RemoveAt(i);
            }
            // Insert / update / reorder.
            for (int i = 0; i < fresh.Count; i++)
            {
                var idx = SortedPeers.IndexOf(fresh[i]);
                if (idx < 0)
                {
                    SortedPeers.Insert(Math.Min(i, SortedPeers.Count), fresh[i]);
                }
                else
                {
                    // Mutate the existing instance so the DataGrid keeps its
                    // SelectedItem reference; INotifyPropertyChanged on PeerStatus
                    // refreshes the bound cell content.
                    SortedPeers[idx].UpdateFrom(fresh[i]);
                    if (idx != i) SortedPeers.Move(idx, i);
                }
            }
        }

        if (Dispatcher.UIThread.CheckAccess()) Apply();
        else Dispatcher.UIThread.Post(Apply);
    }

    public IReadOnlyList<PeerStatus> ExitNodeCandidates
    {
        get
        {
            if (_status?.Peer is null) return Array.Empty<PeerStatus>();
            return _status.Peer.Values
                .Where(p => p.ExitNodeOption)
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public string CurrentExitNodeName
    {
        get
        {
            var id = _prefs?.ExitNodeID;
            if (string.IsNullOrEmpty(id)) return "(none)";
            var peer = _status?.Peer?.Values.FirstOrDefault(p => p.ID == id);
            return peer?.DisplayName ?? id;
        }
    }

    public bool WantRunning => _prefs?.WantRunning ?? false;
    public bool AcceptRoutes => _prefs?.RouteAll ?? false;
    public bool AcceptDns => _prefs?.CorpDNS ?? false;
    public bool ShieldsUp => _prefs?.ShieldsUp ?? false;
    public string ExitNodeId => _prefs?.ExitNodeID ?? "";
    public bool ExitNodeAllowLan => _prefs?.ExitNodeAllowLANAccess ?? false;
    public bool RunSsh => _prefs?.RunSSH ?? false;
    public IReadOnlyList<string> AdvertisedRoutes => _prefs?.AdvertiseRoutes ?? new List<string>();

    // ─────────────────── Lifecycle ───────────────────

    /// <summary>Initial state load + start background bus pump.</summary>
    public async Task StartAsync()
    {
        await RefreshAsync().ConfigureAwait(false);
        _busTask = Task.Run(() => PumpBusAsync(_cts.Token));
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            Status = await _api.GetStatusAsync(ct).ConfigureAwait(false);
            Prefs = await _api.GetPrefsAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(_status?.AuthURL))
                AuthUrl = _status!.AuthURL;
            LastError = null;
        }
        catch (Exception ex) when (ct.IsCancellationRequested is false)
        {
            LastError = ex.Message;
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        // Status polling is a safety net in case the bus stream misses something
        // (or fails entirely). 5s is well under the time a user notices stale state.
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            await RefreshAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task PumpBusAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var mask = IpnNotifyMask.InitialState
                         | IpnNotifyMask.NetMap
                         | IpnNotifyMask.Prefs
                         | IpnNotifyMask.Engine
                         | IpnNotifyMask.InitialHealthState;
                await foreach (var notify in _api.WatchIpnBusAsync(mask, ct).ConfigureAwait(false))
                {
                    HandleNotify(notify);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                LastError = $"IPN bus disconnected: {ex.Message}";
                try { await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private void HandleNotify(IpnNotify notify)
    {
        if (notify.Prefs is not null) Prefs = notify.Prefs;
        if (notify.BrowseToURL is not null) AuthUrl = notify.BrowseToURL.URL;
        if (notify.ErrMessage is not null) LastError = notify.ErrMessage;
        if (notify.State is not null || notify.NetMap is not null)
        {
            // Cheap to ask for full status when something material changed.
            _ = RefreshStatusOnlyAsync();
        }
    }

    private async Task RefreshStatusOnlyAsync()
    {
        try { Status = await _api.GetStatusAsync(_cts.Token).ConfigureAwait(false); }
        catch { /* surfaced via poll loop */ }
    }

    // ─────────────────── Mutating actions ───────────────────

    public async Task LoginAsync()
    {
        IsConnecting = true;
        try
        {
            AuthUrl = null;
            await _api.StartLoginInteractiveAsync(_cts.Token).ConfigureAwait(false);
            await RefreshAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) { LastError = ex.Message; }
        finally { IsConnecting = false; }
    }

    public async Task LogoutAsync()
    {
        try { await _api.LogoutAsync(_cts.Token).ConfigureAwait(false); }
        catch (Exception ex) { LastError = ex.Message; }
        await RefreshAsync(_cts.Token).ConfigureAwait(false);
    }

    public async Task ConnectAsync()
    {
        IsConnecting = true;
        try { Prefs = await _api.UpAsync(_cts.Token).ConfigureAwait(false); }
        catch (Exception ex) { LastError = ex.Message; }
        finally { IsConnecting = false; }
        await RefreshAsync(_cts.Token).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        try { Prefs = await _api.DownAsync(_cts.Token).ConfigureAwait(false); }
        catch (Exception ex) { LastError = ex.Message; }
        await RefreshAsync(_cts.Token).ConfigureAwait(false);
    }

    public Task SetExitNodeAsync(string? stableNodeId) =>
        EditAsync(MaskedPrefs.SetExitNode(stableNodeId));

    public Task SetAcceptRoutesAsync(bool value) =>
        EditAsync(MaskedPrefs.SetRouteAll(value));

    public Task SetAcceptDnsAsync(bool value) =>
        EditAsync(MaskedPrefs.SetCorpDNS(value));

    public Task SetShieldsUpAsync(bool value) =>
        EditAsync(MaskedPrefs.SetShieldsUp(value));

    public Task SetExitNodeAllowLanAsync(bool value) =>
        EditAsync(MaskedPrefs.SetExitNodeAllowLan(value));

    public Task SetAdvertiseRoutesAsync(IEnumerable<string> routes) =>
        EditAsync(MaskedPrefs.SetAdvertiseRoutes(routes));

    public Task SetRunSshAsync(bool value) =>
        EditAsync(MaskedPrefs.SetRunSSH(value));

    public Task SetHostnameAsync(string hostname) =>
        EditAsync(MaskedPrefs.SetHostname(hostname));

    public async Task<PingResult?> PingAsync(string ip)
    {
        try { return await _api.PingAsync(ip, ct: _cts.Token).ConfigureAwait(false); }
        catch (Exception ex) { LastError = ex.Message; return null; }
    }

    // ─────────────────── Taildrop ───────────────────

    public Task<List<TaildropFile>> ListTaildropFilesAsync() =>
        _api.ListTaildropFilesAsync(_cts.Token);

    public Task<Stream> DownloadTaildropFileAsync(string name) =>
        _api.DownloadTaildropFileAsync(name, _cts.Token);

    public Task DeleteTaildropFileAsync(string name) =>
        _api.DeleteTaildropFileAsync(name, _cts.Token);

    public Task SendTaildropFileAsync(string peerId, string fileName, Stream content, long? length = null) =>
        _api.SendTaildropFileAsync(peerId, fileName, content, length, _cts.Token);

    // ─────────────────── private ───────────────────

    private async Task EditAsync(MaskedPrefs patch)
    {
        try { Prefs = await _api.EditPrefsAsync(patch, _cts.Token).ConfigureAwait(false); }
        catch (Exception ex) { LastError = ex.Message; }
        await RefreshAsync(_cts.Token).ConfigureAwait(false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _cts.Cancel();
        try { _busTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _pollTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
        _api.Dispose();
    }
}
