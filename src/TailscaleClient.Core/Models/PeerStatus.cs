using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TailscaleClient.Core.Models;

/// <summary>
/// Mirrors <c>ipnstate.PeerStatus</c>. Many slice/view fields on the Go side
/// are optional; nullable lists keep that semantics.
/// </summary>
public sealed class PeerStatus : IEquatable<PeerStatus>, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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

    /// <summary>Full DNS name (FQDN, trailing dot stripped). Used as the
    /// subtitle / detail row in the UI.</summary>
    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrEmpty(DNSName) ? DNSName.TrimEnd('.') :
        !string.IsNullOrEmpty(HostName) ? HostName : ID;

    /// <summary>Short hostname — what the user actually thinks of the machine as.
    /// Falls back to the first label of the FQDN, then to ID.</summary>
    [JsonIgnore]
    public string ShortName
    {
        get
        {
            if (!string.IsNullOrEmpty(HostName)) return HostName;
            if (!string.IsNullOrEmpty(DNSName))
            {
                var trimmed = DNSName.TrimEnd('.');
                var dot = trimmed.IndexOf('.');
                return dot > 0 ? trimmed[..dot] : trimmed;
            }
            return ID;
        }
    }

    /// <summary>True when this peer can be selected as an exit node.</summary>
    [JsonIgnore]
    public bool CanBeExitNode => ExitNodeOption;

    /// <summary>First IPv4 (100.x.x.x style) Tailscale address, if any.</summary>
    [JsonIgnore]
    public string IPv4 => TailscaleIPs.FirstOrDefault(ip => !ip.Contains(':') && ip.Contains('.')) ?? "";

    /// <summary>First IPv6 (fd7a:...) Tailscale address, if any.</summary>
    [JsonIgnore]
    public string IPv6 => TailscaleIPs.FirstOrDefault(ip => ip.Contains(':')) ?? "";

    // Identity is the StableNodeID. Two PeerStatus instances with the same ID
    // are the same peer — even if one is a freshly-deserialized snapshot from
    // the next poll. This lets data-bound controls (DataGrid, ListBox, etc.)
    // preserve selection across periodic refreshes.
    public bool Equals(PeerStatus? other) =>
        other is not null && !string.IsNullOrEmpty(ID) && ID == other.ID;

    public override bool Equals(object? obj) => Equals(obj as PeerStatus);

    public override int GetHashCode() => ID?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <summary>
    /// Copy mutable fields from a freshly-deserialized snapshot of the same peer
    /// (matched by ID) and notify bindings. Used by <c>TailscaleService</c> to
    /// update the live <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>
    /// in place — replacing the instance would invalidate selection in the UI.
    /// </summary>
    public void UpdateFrom(PeerStatus fresh)
    {
        PublicKey = fresh.PublicKey;
        HostName = fresh.HostName;
        DNSName = fresh.DNSName;
        OS = fresh.OS;
        UserID = fresh.UserID;
        AltSharerUserID = fresh.AltSharerUserID;
        TailscaleIPs = fresh.TailscaleIPs;
        AllowedIPs = fresh.AllowedIPs;
        Tags = fresh.Tags;
        PrimaryRoutes = fresh.PrimaryRoutes;
        Addrs = fresh.Addrs;
        CurAddr = fresh.CurAddr;
        Relay = fresh.Relay;
        PeerRelay = fresh.PeerRelay;
        RxBytes = fresh.RxBytes;
        TxBytes = fresh.TxBytes;
        Created = fresh.Created;
        LastWrite = fresh.LastWrite;
        LastSeen = fresh.LastSeen;
        LastHandshake = fresh.LastHandshake;
        Online = fresh.Online;
        ExitNode = fresh.ExitNode;
        ExitNodeOption = fresh.ExitNodeOption;
        Active = fresh.Active;
        PeerAPIURL = fresh.PeerAPIURL;
        TaildropTarget = fresh.TaildropTarget;
        NoFileSharingReason = fresh.NoFileSharingReason;
        InNetworkMap = fresh.InNetworkMap;
        InMagicSock = fresh.InMagicSock;
        InEngine = fresh.InEngine;
        Expired = fresh.Expired;
        KeyExpiry = fresh.KeyExpiry;
        Location = fresh.Location;
        ShareeNode = fresh.ShareeNode;
        // Empty string = "all properties may have changed" — bindings re-read.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}

public sealed class Location
{
    public string Country { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string City { get; set; } = "";
    public string CityCode { get; set; } = "";
    public int Priority { get; set; }
}
