using System.Text.Json.Serialization;

namespace TailscaleClient.Core.Models;

/// <summary>
/// Mirrors <c>ipnstate.PeerStatus</c>. Many slice/view fields on the Go side
/// are optional; nullable lists keep that semantics.
/// </summary>
public sealed class PeerStatus
{
    public string ID { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string HostName { get; set; } = "";
    public string DNSName { get; set; } = "";
    public string OS { get; set; } = "";
    public long UserID { get; set; }
    public long AltSharerUserID { get; set; }
    public List<string> TailscaleIPs { get; set; } = new();
    public List<string>? AllowedIPs { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? PrimaryRoutes { get; set; }
    public List<string> Addrs { get; set; } = new();
    public string CurAddr { get; set; } = "";
    public string Relay { get; set; } = "";
    public string PeerRelay { get; set; } = "";
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset LastWrite { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset LastHandshake { get; set; }
    public bool Online { get; set; }
    public bool ExitNode { get; set; }
    public bool ExitNodeOption { get; set; }
    public bool Active { get; set; }
    public List<string>? PeerAPIURL { get; set; }
    public int TaildropTarget { get; set; }
    public string NoFileSharingReason { get; set; } = "";
    public bool InNetworkMap { get; set; }
    public bool InMagicSock { get; set; }
    public bool InEngine { get; set; }
    public bool Expired { get; set; }
    public DateTimeOffset? KeyExpiry { get; set; }
    public Location? Location { get; set; }
    public bool ShareeNode { get; set; }

    /// <summary>Best display name — DNSName trimmed of the MagicDNS suffix, or HostName.</summary>
    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrEmpty(DNSName) ? DNSName.TrimEnd('.') :
        !string.IsNullOrEmpty(HostName) ? HostName : ID;

    /// <summary>True when this peer can be selected as an exit node.</summary>
    [JsonIgnore]
    public bool CanBeExitNode => ExitNodeOption;
}

public sealed class Location
{
    public string Country { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string City { get; set; } = "";
    public string CityCode { get; set; } = "";
    public int Priority { get; set; }
}
