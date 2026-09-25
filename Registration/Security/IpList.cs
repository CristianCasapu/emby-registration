using System.Net;

namespace Registration.Security;

/// <summary>Lista de adrese si retele (CIDR) din configuratie, una pe linie.</summary>
public sealed class IpList
{
    private readonly List<(byte[] Network, int Prefix)> _entries = new();

    public IpList(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var slash = line.IndexOf('/');
            var address = slash < 0 ? line : line[..slash];
            if (!IPAddress.TryParse(address, out var ip))
            {
                continue;
            }

            var bytes = RateLimiter.Normalize(ip)!.GetAddressBytes();
            var prefix = bytes.Length * 8;
            if (slash >= 0 && (!int.TryParse(line[(slash + 1)..], out prefix) || prefix < 0 || prefix > bytes.Length * 8))
            {
                continue;
            }

            _entries.Add((bytes, prefix));
        }
    }

    public bool Contains(IPAddress? ip)
    {
        ip = RateLimiter.Normalize(ip);
        if (ip == null)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return _entries.Any(e => e.Network.Length == bytes.Length && Matches(e.Network, bytes, e.Prefix));
    }

    private static bool Matches(byte[] network, byte[] address, int prefix)
    {
        var full = prefix / 8;
        for (var i = 0; i < full; i++)
        {
            if (network[i] != address[i])
            {
                return false;
            }
        }

        var rest = prefix % 8;
        if (rest == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - rest));
        return (network[full] & mask) == (address[full] & mask);
    }
}
