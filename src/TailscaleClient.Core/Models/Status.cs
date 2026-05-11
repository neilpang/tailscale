using System.Text.Json.Serialization;

namespace TailscaleClient.Core.Models;

/// <summary>
/// Response shape for <c>GET /localapi/v0/status</c>. Mirrors
/// <c>ipnstate.Status</c> on the Go side. Field names match the Go JSON tags
/// (default Go capitalization, no rename). IP addresses are kept as strings
/// because we only ever display them; no parsing is needed.
/// </summary>
public sealed class Status
{
    public string Version { get; set; } = "";
    public bool TUN { get; set; }
    public string BackendState { get; set; } = "";
    public bool HaveNodeKey { get; set; }
    public string AuthURL { get; set; } = "";
    public List<string> TailscaleIPs { get; set; } = new();
    public PeerStatus? Self { get; set; }
    public ExitNodeStatus? ExitNodeStatus { get; set; }
    public List<string> Health { get; set; } = new();
    public string MagicDNSSuffix { get; set; } = "";
    public TailnetStatus? CurrentTailnet { get; set; }
    public List<string> CertDomains { get; set; } = new();
    public Dictionary<string, PeerStatus> Peer { get; set; } = new();
    public Dictionary<string, UserProfile> User { get; set; } = new();
    public ClientVersion? ClientVersion { get; set; }
}

public sealed class TailnetStatus
{
    public string Name { get; set; } = "";
    public string MagicDNSSuffix { get; set; } = "";
    public bool MagicDNSEnabled { get; set; }
}

public sealed class ExitNodeStatus
{
    public string ID { get; set; } = "";
    public bool Online { get; set; }
    public List<string> TailscaleIPs { get; set; } = new();
}

public sealed class UserProfile
{
    public long ID { get; set; }
    public string LoginName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ProfilePicURL { get; set; } = "";
}

public sealed class ClientVersion
{
    public bool RunningLatest { get; set; }
    public string LatestVersion { get; set; } = "";
    public string UrgentSecurityUpdate { get; set; } = "";
    public string Notify { get; set; } = "";
    public string NotifyURL { get; set; } = "";
    public string NotifyText { get; set; } = "";
}
