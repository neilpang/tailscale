using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TailscaleClient.Core.Models;

namespace TailscaleClient.Core.LocalApi;

/// <summary>
/// Strongly-typed wrapper around the Tailscale LocalAPI. One instance per
/// process is fine — <see cref="HttpClient"/> connection pooling handles
/// concurrency. <see cref="WatchIpnBusAsync"/> opens its own connection.
/// </summary>
public sealed class LocalApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public LocalApiClient() : this(LocalApiHttpFactory.Create(), ownsHttpClient: true) { }

    public LocalApiClient(HttpClient http, bool ownsHttpClient = false)
    {
        _http = http;
        _ownsHttpClient = ownsHttpClient;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var o = new JsonSerializerOptions
        {
            // Go default casing — keep property names as-declared.
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        return o;
    }

    // ─────────────────── Status / metadata ───────────────────

    public async Task<Status> GetStatusAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("/localapi/v0/status", ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<Status>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    // ─────────────────── Preferences ───────────────────

    public async Task<Prefs> GetPrefsAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("/localapi/v0/prefs", ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<Prefs>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    public async Task<Prefs> EditPrefsAsync(MaskedPrefs patch, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(patch, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Patch, "/localapi/v0/prefs") { Content = content };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<Prefs>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    // ─────────────────── Up / Down convenience ───────────────────

    public Task<Prefs> UpAsync(CancellationToken ct = default) =>
        EditPrefsAsync(MaskedPrefs.SetWantRunning(true), ct);

    public Task<Prefs> DownAsync(CancellationToken ct = default) =>
        EditPrefsAsync(MaskedPrefs.SetWantRunning(false), ct);

    // ─────────────────── Login / Logout ───────────────────

    /// <summary>
    /// Triggers tailscaled to (re)start the interactive login flow. The auth URL
    /// will subsequently arrive over the IPN bus as <c>BrowseToURL</c> or
    /// surface in <see cref="Status.AuthURL"/>.
    /// </summary>
    public async Task StartLoginInteractiveAsync(CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync("/localapi/v0/login-interactive", content: null, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync("/localapi/v0/logout", content: null, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
    }

    // ─────────────────── Ping ───────────────────

    public async Task<PingResult> PingAsync(string ip, string pingType = "disco", CancellationToken ct = default)
    {
        var url = $"/localapi/v0/ping?ip={Uri.EscapeDataString(ip)}&type={Uri.EscapeDataString(pingType)}";
        using var resp = await _http.PostAsync(url, content: null, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<PingResult>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    // ─────────────────── Taildrop ───────────────────

    public async Task<List<TaildropFile>> ListTaildropFilesAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("/localapi/v0/files/", ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<List<TaildropFile>>(JsonOptions, ct).ConfigureAwait(false))
               ?? new List<TaildropFile>();
    }

    public async Task<Stream> DownloadTaildropFileAsync(string name, CancellationToken ct = default)
    {
        var url = $"/localapi/v0/files/{Uri.EscapeDataString(name)}";
        var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        return await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteTaildropFileAsync(string name, CancellationToken ct = default)
    {
        var url = $"/localapi/v0/files/{Uri.EscapeDataString(name)}";
        using var resp = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
    }

    /// <summary>Send a file to a peer. <paramref name="peerStableId"/> is the peer's StableNodeID.</summary>
    public async Task SendTaildropFileAsync(
        string peerStableId,
        string fileName,
        Stream content,
        long? contentLength = null,
        CancellationToken ct = default)
    {
        var url = $"/localapi/v0/file-put/{Uri.EscapeDataString(peerStableId)}/{Uri.EscapeDataString(fileName)}";
        using var streamContent = new StreamContent(content);
        if (contentLength.HasValue)
            streamContent.Headers.ContentLength = contentLength.Value;
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        using var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = streamContent };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
    }

    // ─────────────────── IPN bus stream ───────────────────

    /// <summary>
    /// Long-running stream of <see cref="IpnNotify"/> events. Each event is a
    /// JSON object on its own line. Disposes the underlying HTTP response when
    /// the enumeration ends or is cancelled.
    /// </summary>
    public async IAsyncEnumerable<IpnNotify> WatchIpnBusAsync(
        IpnNotifyMask mask = IpnNotifyMask.InitialState | IpnNotifyMask.NetMap | IpnNotifyMask.Prefs,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"/localapi/v0/watch-ipn-bus?mask={(int)mask}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await EnsureSuccess(resp, ct).ConfigureAwait(false);
        using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) yield break;
            if (line.Length == 0) continue;
            IpnNotify? evt;
            try
            {
                evt = JsonSerializer.Deserialize<IpnNotify>(line, JsonOptions);
            }
            catch (JsonException)
            {
                // Skip malformed line rather than tearing down the stream.
                continue;
            }
            if (evt is not null) yield return evt;
        }
    }

    // ─────────────────── helpers ───────────────────

    private static async Task EnsureSuccess(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        string? body = null;
        try { body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
        catch { /* ignored */ }
        throw new LocalApiException(
            resp.StatusCode,
            body,
            $"LocalAPI {resp.RequestMessage?.Method} {resp.RequestMessage?.RequestUri} failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}");
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _http.Dispose();
    }
}

[Flags]
public enum IpnNotifyMask
{
    None = 0,
    InitialState = 1 << 0,
    NetMap = 1 << 1,
    Prefs = 1 << 2,
    Engine = 1 << 3,
    NoPrivateKeys = 1 << 4,
    InitialNetMap = 1 << 5,
    InitialDriveShares = 1 << 6,
    InitialOutgoingFiles = 1 << 7,
    InitialHealthState = 1 << 8,
    RateLimitNetMaps = 1 << 9,
}

public sealed class TaildropFile
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public bool PartialPath { get; set; }
}
