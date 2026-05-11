using System.Text.Json.Serialization;

namespace TailscaleClient.Core.Models;

/// <summary>
/// Body for <c>PATCH /localapi/v0/prefs</c>. Each <c>*Set</c> sibling field
/// must be true for the corresponding <see cref="Prefs"/> field to be applied.
/// We expose strongly-typed helpers to avoid the easy mistake of changing a
/// field without setting its mask bit.
/// </summary>
public sealed class MaskedPrefs
{
    // Embedded Prefs — Go's struct embedding flattens these into the same JSON object.
    public string? ControlURL { get; set; }
    public bool RouteAll { get; set; }
    public string? ExitNodeID { get; set; }
    public string? ExitNodeIP { get; set; }
    public bool ExitNodeAllowLANAccess { get; set; }
    public bool CorpDNS { get; set; }
    public bool RunSSH { get; set; }
    public bool RunWebClient { get; set; }
    public bool WantRunning { get; set; }
    public bool LoggedOut { get; set; }
    public bool ShieldsUp { get; set; }
    public List<string>? AdvertiseTags { get; set; }
    public string? Hostname { get; set; }
    public bool NotepadURLs { get; set; }
    public bool ForceDaemon { get; set; }
    public List<string>? AdvertiseRoutes { get; set; }
    public List<string>? AdvertiseServices { get; set; }
    public bool NoSNAT { get; set; }
    public string? OperatorUser { get; set; }
    public string? ProfileName { get; set; }
    public AutoUpdatePrefs? AutoUpdate { get; set; }
    public AppConnectorPrefs? AppConnector { get; set; }
    public bool PostureChecking { get; set; }
    public string? NetfilterKind { get; set; }

    public bool ControlURLSet { get; set; }
    public bool RouteAllSet { get; set; }
    public bool ExitNodeIDSet { get; set; }
    public bool ExitNodeIPSet { get; set; }
    public bool ExitNodeAllowLANAccessSet { get; set; }
    public bool CorpDNSSet { get; set; }
    public bool RunSSHSet { get; set; }
    public bool RunWebClientSet { get; set; }
    public bool WantRunningSet { get; set; }
    public bool LoggedOutSet { get; set; }
    public bool ShieldsUpSet { get; set; }
    public bool AdvertiseTagsSet { get; set; }
    public bool HostnameSet { get; set; }
    public bool NotepadURLsSet { get; set; }
    public bool ForceDaemonSet { get; set; }
    public bool AdvertiseRoutesSet { get; set; }
    public bool AdvertiseServicesSet { get; set; }
    public bool NoSNATSet { get; set; }
    public bool OperatorUserSet { get; set; }
    public bool ProfileNameSet { get; set; }
    public AutoUpdatePrefsMask? AutoUpdateSet { get; set; }
    public bool AppConnectorSet { get; set; }
    public bool PostureCheckingSet { get; set; }
    public bool NetfilterKindSet { get; set; }

    // ─────────────── Strongly-typed builder helpers ───────────────

    public static MaskedPrefs SetWantRunning(bool value) =>
        new() { WantRunning = value, WantRunningSet = true };

    public static MaskedPrefs SetExitNode(string? stableNodeId)
    {
        // Empty string clears the exit node; tailscaled accepts ExitNodeID="" as "no exit node".
        return new MaskedPrefs
        {
            ExitNodeID = stableNodeId ?? "",
            ExitNodeIDSet = true,
            ExitNodeIP = "",
            ExitNodeIPSet = true,
        };
    }

    public static MaskedPrefs SetExitNodeAllowLan(bool value) =>
        new() { ExitNodeAllowLANAccess = value, ExitNodeAllowLANAccessSet = true };

    public static MaskedPrefs SetRouteAll(bool value) =>
        new() { RouteAll = value, RouteAllSet = true };

    public static MaskedPrefs SetCorpDNS(bool value) =>
        new() { CorpDNS = value, CorpDNSSet = true };

    public static MaskedPrefs SetShieldsUp(bool value) =>
        new() { ShieldsUp = value, ShieldsUpSet = true };

    public static MaskedPrefs SetAdvertiseRoutes(IEnumerable<string> routes) =>
        new() { AdvertiseRoutes = routes.ToList(), AdvertiseRoutesSet = true };

    public static MaskedPrefs SetHostname(string hostname) =>
        new() { Hostname = hostname, HostnameSet = true };

    public static MaskedPrefs SetRunSSH(bool value) =>
        new() { RunSSH = value, RunSSHSet = true };
}

public sealed class AutoUpdatePrefsMask
{
    public bool CheckSet { get; set; }
    public bool ApplySet { get; set; }
}
