namespace TailscaleClient.Core.Models;

/// <summary>Response from <c>POST /localapi/v0/ping</c>. Mirrors <c>ipnstate.PingResult</c>.</summary>
public sealed class PingResult
{
    public string IP { get; set; } = "";
    public string NodeIP { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string Err { get; set; } = "";
    public double LatencySeconds { get; set; }
    public string Endpoint { get; set; } = "";
    public int DERPRegionID { get; set; }
    public string DERPRegionCode { get; set; } = "";
    public int PeerAPIPort { get; set; }
    public bool IsLocalIP { get; set; }
}
