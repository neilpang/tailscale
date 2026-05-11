using TailscaleClient.Core.LocalApi;

Console.WriteLine("== Tailscale LocalAPI smoke test ==");
Console.WriteLine();

using var client = new LocalApiClient();

// --ping <ip> [<type>] : exercise the ping endpoint for diagnosis
var pingIdx = Array.IndexOf(args, "--ping");
if (pingIdx >= 0 && pingIdx + 1 < args.Length)
{
    var ip = args[pingIdx + 1];
    var types = (pingIdx + 2 < args.Length && !args[pingIdx + 2].StartsWith("--"))
        ? new[] { args[pingIdx + 2] }
        : new[] { "disco", "TSMP", "ICMP" };
    foreach (var t in types)
    {
        Console.WriteLine($"-> POST /ping?ip={ip}&type={t}");
        try
        {
            var pr = await client.PingAsync(ip, t);
            Console.WriteLine($"   IP={pr.IP}");
            Console.WriteLine($"   NodeName={pr.NodeName}");
            Console.WriteLine($"   Err={(string.IsNullOrEmpty(pr.Err) ? "(none)" : pr.Err)}");
            Console.WriteLine($"   LatencySeconds={pr.LatencySeconds}");
            Console.WriteLine($"   Endpoint={pr.Endpoint}");
            Console.WriteLine($"   DERPRegion={pr.DERPRegionCode}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"   THROW: {ex.GetType().Name}: {ex.Message}");
        }
        Console.WriteLine();
    }
    return 0;
}

try
{
    Console.WriteLine("-> GET /status");
    var status = await client.GetStatusAsync();
    Console.WriteLine($"  Version       : {status.Version}");
    Console.WriteLine($"  BackendState  : {status.BackendState}");
    Console.WriteLine($"  TUN           : {status.TUN}");
    Console.WriteLine($"  AuthURL       : {(string.IsNullOrEmpty(status.AuthURL) ? "(none)" : status.AuthURL)}");
    Console.WriteLine($"  Self          : {status.Self?.DisplayName ?? "(none)"}");
    Console.WriteLine($"  Tailnet       : {status.CurrentTailnet?.Name ?? "(none)"}");
    Console.WriteLine($"  MagicDNS      : {status.CurrentTailnet?.MagicDNSEnabled}");
    Console.WriteLine($"  Self IPs      : {string.Join(", ", status.Self?.TailscaleIPs ?? new())}");
    Console.WriteLine($"  Peers         : {status.Peer.Count}");
    foreach (var p in status.Peer.Values.OrderByDescending(p => p.Online).Take(8))
    {
        var marker = p.Online ? "*" : "-";
        var ip = p.TailscaleIPs.Count > 0 ? p.TailscaleIPs[0] : "-";
        var exit = p.ExitNodeOption ? " [exit-node]" : "";
        Console.WriteLine($"    {marker} {p.DisplayName,-30} {ip,-18} {p.OS,-8}{exit}");
    }
    if (status.Peer.Count > 8) Console.WriteLine($"    ... and {status.Peer.Count - 8} more");
    Console.WriteLine();

    Console.WriteLine("-> GET /prefs");
    var prefs = await client.GetPrefsAsync();
    Console.WriteLine($"  ControlURL    : {prefs.ControlURL}");
    Console.WriteLine($"  WantRunning   : {prefs.WantRunning}");
    Console.WriteLine($"  RouteAll      : {prefs.RouteAll}");
    Console.WriteLine($"  CorpDNS       : {prefs.CorpDNS}");
    Console.WriteLine($"  ShieldsUp     : {prefs.ShieldsUp}");
    Console.WriteLine($"  ExitNodeID    : {(string.IsNullOrEmpty(prefs.ExitNodeID) ? "(none)" : prefs.ExitNodeID)}");
    Console.WriteLine($"  Hostname      : {prefs.Hostname}");
    Console.WriteLine();

    if (args.Contains("--watch"))
    {
        Console.WriteLine("-> GET /watch-ipn-bus (press Ctrl+C to stop)");
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        var count = 0;
        await foreach (var n in client.WatchIpnBusAsync(ct: cts.Token))
        {
            count++;
            var what = new List<string>();
            if (n.State is not null) what.Add($"state={n.State}");
            if (n.NetMap is not null) what.Add("netmap");
            if (n.Prefs is not null) what.Add("prefs");
            if (n.Engine is not null) what.Add($"engine(rx={n.Engine.RxBytes},tx={n.Engine.TxBytes})");
            if (n.BrowseToURL is not null) what.Add($"browse={n.BrowseToURL.URL}");
            if (n.ErrMessage is not null) what.Add($"err={n.ErrMessage}");
            Console.WriteLine($"  [{count}] {string.Join(' ', what)}");
        }
    }

    Console.WriteLine("OK");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("FAILED: " + ex.GetType().Name);
    Console.Error.WriteLine(ex.Message);
    if (ex.InnerException is not null)
        Console.Error.WriteLine("Inner: " + ex.InnerException.Message);
    return 1;
}
