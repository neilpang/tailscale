using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TailscaleClient.UI.Services;

public sealed record LocalCidr(string InterfaceName, string Cidr);

public static class LocalNetworkInfo
{
    public static List<LocalCidr> GetLocalCidrs()
    {
        var result = new List<LocalCidr>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            if (IsVpnInterface(nic.Name) || IsVpnInterface(nic.Description)) continue;

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                var ip = addr.Address;
                if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ip)) continue;

                var bytes = ip.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254) continue;         // link-local
                if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) // CGNAT — Tailscale
                    continue;

                var prefix = addr.PrefixLength;
                if (prefix <= 0 || prefix >= 32) continue;                // skip /32 single-host (VPN tunnels)

                var cidr = ToCidr(bytes, prefix);
                var key = $"{nic.Name}|{cidr}";
                if (!seen.Add(key)) continue;

                result.Add(new LocalCidr(nic.Name, cidr));
            }
        }
        return result;
    }

    private static readonly string[] VpnKeywords =
        { "Tailscale", "WireGuard", "OpenVPN", "ZeroTier" };

    private static bool IsVpnInterface(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var kw in VpnKeywords)
            if (name.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string ToCidr(byte[] ipBytes, int prefix)
    {
        var network = new byte[ipBytes.Length];
        int remaining = prefix;
        for (int i = 0; i < ipBytes.Length; i++)
        {
            if (remaining >= 8) { network[i] = ipBytes[i]; remaining -= 8; }
            else if (remaining > 0)
            {
                byte mask = (byte)(0xFF << (8 - remaining));
                network[i] = (byte)(ipBytes[i] & mask);
                remaining = 0;
            }
            else network[i] = 0;
        }
        return $"{new IPAddress(network)}/{prefix}";
    }
}
