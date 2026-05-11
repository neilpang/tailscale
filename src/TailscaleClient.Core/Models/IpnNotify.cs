namespace TailscaleClient.Core.Models;

/// <summary>
/// Event delivered by <c>GET /localapi/v0/watch-ipn-bus</c>. Mirrors
/// <c>ipn.Notify</c>. Fields are individually optional; only those that
/// changed in a given tick are populated.
/// </summary>
public sealed class IpnNotify
{
    public int Version { get; set; }
    public string? ErrMessage { get; set; }
    public string? LoginFinished { get; set; }
    public string? State { get; set; }
    public Prefs? Prefs { get; set; }
    public NetMap? NetMap { get; set; }
    public Engine? Engine { get; set; }
    public BrowseToURL? BrowseToURL { get; set; }
    public string? BackendLogID { get; set; }
    public FilesWaiting? FilesWaiting { get; set; }
    public List<IncomingFile>? IncomingFiles { get; set; }
    public Status? Status { get; set; }
    public Health? Health { get; set; }
}

public sealed class NetMap
{
    public string? SelfNode { get; set; }
    public string? NodeKey { get; set; }
    public string? DomainName { get; set; }
    public string? MachineStatus { get; set; }
}

public sealed class Engine
{
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public int NumLive { get; set; }
}

public sealed class BrowseToURL
{
    public string URL { get; set; } = "";
}

public sealed class FilesWaiting { }

public sealed class IncomingFile
{
    public string Name { get; set; } = "";
    public DateTimeOffset Started { get; set; }
    public long DeclaredSize { get; set; }
    public long Received { get; set; }
    public bool PartialPath { get; set; }
    public string? FinalPath { get; set; }
}

public sealed class Health
{
    public List<string>? Warnings { get; set; }
}
