using System.Text.Json;
using System.Text.Json.Serialization;

namespace TailscaleClient.Core.Models;

/// <summary>
/// Mirrors <c>ipn.Prefs</c>. Returned by <c>GET /localapi/v0/prefs</c>.
/// </summary>
public sealed class Prefs
{
    public string ControlURL { get; set; } = "";
    public bool RouteAll { get; set; }
    public string ExitNodeID { get; set; } = "";
    public string ExitNodeIP { get; set; } = "";
    public bool ExitNodeAllowLANAccess { get; set; }
    public bool CorpDNS { get; set; }
    public bool RunSSH { get; set; }
    public bool RunWebClient { get; set; }
    public bool WantRunning { get; set; }
    public bool LoggedOut { get; set; }
    public bool ShieldsUp { get; set; }
    public List<string>? AdvertiseTags { get; set; }
    public string Hostname { get; set; } = "";
    public bool NotepadURLs { get; set; }
    public bool ForceDaemon { get; set; }
    public List<string>? AdvertiseRoutes { get; set; }
    public List<string>? AdvertiseServices { get; set; }
    public bool NoSNAT { get; set; }
    public string OperatorUser { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public AutoUpdatePrefs AutoUpdate { get; set; } = new();
    public AppConnectorPrefs AppConnector { get; set; } = new();
    public bool PostureChecking { get; set; }
    public string NetfilterKind { get; set; } = "";

    /// <summary>Persisted machine identity ("Config" in the JSON). Kept opaque —
    /// the UI never inspects its contents, and the shape varies by daemon version.</summary>
    [JsonPropertyName("Config")]
    public JsonElement? Persist { get; set; }
}

public sealed class AutoUpdatePrefs
{
    public bool Check { get; set; }
    public bool? Apply { get; set; }
}

public sealed class AppConnectorPrefs
{
    public bool Advertise { get; set; }
}
