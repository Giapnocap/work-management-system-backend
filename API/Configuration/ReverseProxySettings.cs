using System.Net;
using System.Net.Sockets;
using ForwardedIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace WorkManagementSystem.API.Configuration;

public sealed class ReverseProxySettings
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }
    public int ForwardLimit { get; set; } = 1;
    public string[] KnownProxies { get; set; } = Array.Empty<string>();
    public string[] KnownNetworks { get; set; } = Array.Empty<string>();

    public void Validate(bool isProduction)
    {
        if (!Enabled)
            return;

        if (ForwardLimit is < 1 or > 5)
            throw new InvalidOperationException("ReverseProxy:ForwardLimit must be between 1 and 5.");

        _ = ParseKnownProxies();
        _ = ParseKnownNetworks();

        if (isProduction && KnownProxies.Length == 0 && KnownNetworks.Length == 0)
        {
            throw new InvalidOperationException(
                "An enabled production reverse proxy must declare KnownProxies or KnownNetworks.");
        }
    }

    public IReadOnlyList<IPAddress> ParseKnownProxies()
    {
        var addresses = new List<IPAddress>(KnownProxies.Length);
        foreach (var value in KnownProxies)
        {
            if (!IPAddress.TryParse(value?.Trim(), out var address))
                throw new InvalidOperationException($"Invalid trusted proxy address: '{value}'.");

            addresses.Add(address);
        }

        return addresses;
    }

    public IReadOnlyList<ForwardedIpNetwork> ParseKnownNetworks()
    {
        var networks = new List<ForwardedIpNetwork>(KnownNetworks.Length);
        foreach (var value in KnownNetworks)
        {
            var parts = value?.Trim().Split('/', 2, StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            if (parts.Length != 2 ||
                !IPAddress.TryParse(parts[0], out var prefix) ||
                !int.TryParse(parts[1], out var prefixLength))
            {
                throw new InvalidOperationException($"Invalid trusted proxy network: '{value}'.");
            }

            var maximumPrefixLength = prefix.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength < 0 || prefixLength > maximumPrefixLength)
                throw new InvalidOperationException($"Invalid trusted proxy network: '{value}'.");

            networks.Add(new ForwardedIpNetwork(prefix, prefixLength));
        }

        return networks;
    }
}
