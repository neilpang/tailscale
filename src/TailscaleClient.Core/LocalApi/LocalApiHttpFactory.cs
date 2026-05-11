using System.IO.Pipes;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace TailscaleClient.Core.LocalApi;

/// <summary>
/// Builds the <see cref="HttpClient"/> that talks to the local Tailscale daemon.
/// Each platform has its own transport quirks:
///
/// <list type="bullet">
/// <item><b>Windows</b> — named pipe at
/// <c>\\.\pipe\ProtectedPrefix\Administrators\Tailscale\tailscaled</c>,
/// opened with <see cref="TokenImpersonationLevel.Impersonation"/> so the daemon
/// can identify the caller via <c>ImpersonateNamedPipeClient</c>.</item>
///
/// <item><b>macOS</b> (App Store / system extension) — scans
/// <c>/Library/Tailscale</c> for a <c>sameuserproof-{port}-{token}</c> file,
/// then connects to <c>127.0.0.1:{port}</c> with Basic auth using the token.
/// Falls back to a Unix socket at <c>/var/run/tailscaled.socket</c> if the
/// sameuserproof file is missing.</item>
///
/// <item><b>Linux</b> — Unix socket at <c>/var/run/tailscale/tailscaled.sock</c>.</item>
/// </list>
/// </summary>
public static class LocalApiHttpFactory
{
    public const string HostHeader = "local-tailscaled.sock";
    public const string WindowsPipeName = @"ProtectedPrefix\Administrators\Tailscale\tailscaled";
    public const string LinuxSocketPath = "/var/run/tailscale/tailscaled.sock";
    public const string MacOsSocketPath = "/var/run/tailscaled.socket";
    public const string MacOsSameUserProofDir = "/Library/Tailscale";

    public static HttpClient Create(TimeSpan? timeout = null)
    {
        if (OperatingSystem.IsWindows()) return CreateWindows(timeout);
        if (OperatingSystem.IsMacOS()) return CreateMacOs(timeout);
        if (OperatingSystem.IsLinux()) return CreateUnixSocket(LinuxSocketPath, timeout);
        throw new PlatformNotSupportedException(
            $"Tailscale LocalAPI transport not implemented for {RuntimeInformation.OSDescription}.");
    }

    // ─────────────────── Windows ───────────────────

    private static HttpClient CreateWindows(TimeSpan? timeout)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var pipe = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: WindowsPipeName,
                    direction: PipeDirection.InOut,
                    options: PipeOptions.Asynchronous,
                    impersonationLevel: TokenImpersonationLevel.Impersonation,
                    inheritability: System.IO.HandleInheritability.None);
                try
                {
                    await pipe.ConnectAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is TimeoutException or UnauthorizedAccessException)
                {
                    pipe.Dispose();
                    throw new HttpRequestException(
                        $"Could not connect to Tailscale named pipe '{WindowsPipeName}'. " +
                        "Is tailscaled running?", ex);
                }
                return pipe;
            },
            MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseProxy = false,
            UseCookies = false,
        };
        return BuildClient(handler, timeout);
    }

    // ─────────────────── macOS ───────────────────

    private static HttpClient CreateMacOs(TimeSpan? timeout)
    {
        // Prefer the sameuserproof file approach (App Store + system extension builds).
        if (TryReadMacOsToken(out var port, out var token))
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            };
            var client = new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
                Timeout = timeout ?? TimeSpan.FromSeconds(30),
            };
            // Empty user, token as password — matches what the official CLI does.
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + token));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
            return client;
        }

        // Fallback for a self-built / open-source tailscaled.
        return CreateUnixSocket(MacOsSocketPath, timeout);
    }

    /// <summary>
    /// Scans <c>/Library/Tailscale</c> for a file named <c>sameuserproof-{port}-{token}</c>
    /// and parses out the port and token. Returns <c>false</c> if no such file
    /// exists or the directory isn't readable.
    /// </summary>
    public static bool TryReadMacOsToken(out int port, out string token)
    {
        port = 0;
        token = "";
        try
        {
            if (!Directory.Exists(MacOsSameUserProofDir)) return false;
            foreach (var path in Directory.EnumerateFiles(MacOsSameUserProofDir, "sameuserproof-*"))
            {
                var name = Path.GetFileName(path);
                // sameuserproof-{port}-{token}
                var parts = name.Split('-', 3);
                if (parts.Length != 3) continue;
                if (!int.TryParse(parts[1], out var p) || p <= 0) continue;
                if (string.IsNullOrEmpty(parts[2])) continue;
                port = p;
                token = parts[2];
                return true;
            }
        }
        catch (UnauthorizedAccessException) { /* not our user / sandboxed */ }
        catch (IOException) { /* race with rename */ }
        return false;
    }

    // ─────────────────── Unix domain socket (Linux + macOS fallback) ───────────────────

    private static HttpClient CreateUnixSocket(string path, TimeSpan? timeout)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var ep = new UnixDomainSocketEndPoint(path);
                var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await sock.ConnectAsync(ep, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is SocketException or UnauthorizedAccessException)
                {
                    sock.Dispose();
                    throw new HttpRequestException(
                        $"Could not connect to Tailscale Unix socket '{path}'. " +
                        "Is tailscaled running? Note: tailscaled normally runs as root; " +
                        "your user may need to be in the tailscale group, or run with sudo.", ex);
                }
                return new NetworkStream(sock, ownsSocket: true);
            },
            MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseProxy = false,
            UseCookies = false,
        };
        return BuildClient(handler, timeout);
    }

    private static HttpClient BuildClient(SocketsHttpHandler handler, TimeSpan? timeout)
    {
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri($"http://{HostHeader}/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
    }
}
