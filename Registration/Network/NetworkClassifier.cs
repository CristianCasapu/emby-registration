using System.Net;
using MediaBrowser.Model.Logging;

namespace Registration.Network;

public sealed record NetworkInfo(long? Asn, string? Organization, string Kind)
{
    public const string Residential = "residential";
    public const string Datacenter = "datacenter";
    public const string Warp = "warp";
    public const string Tor = "tor";
    public const string Local = "local";
    public const string Unknown = "unknown";

    public static readonly NetworkInfo None = new(null, null, Unknown);
}

/// <summary>
/// Ce fel de retea are vizitatorul: acasa/mobil, centru de date sau VPN comercial, Tor,
/// Cloudflare WARP. Furnizorul (ASN) vine din baza GeoLite2-ASN descarcata de plugin-ul
/// Jurnal de acces (sau un fisier ales in setari); fara baza, reteaua ramane „necunoscuta”
/// si nu se blocheaza nimic pe acest criteriu.
/// </summary>
public sealed class NetworkClassifier
{
    /// <summary>Furnizori de gazduire, cloud si VPN comerciale: de acolo nu vin oameni, ci scripturi.</summary>
    private static readonly HashSet<long> DatacenterAsns = new()
    {
        16509, 14618, 8987, // Amazon AWS
        15169, 396982, 19527, // Google Cloud
        8075, 8068, // Microsoft Azure
        14061, // DigitalOcean
        16276, 35540, // OVH
        24940, 213230, // Hetzner
        63949, // Akamai Linode
        20473, // Vultr / Choopa
        51167, // Contabo
        9009, // M247 (multe VPN-uri)
        60781, 28753, 59253, 7203, // Leaseweb
        212238, 60068, // Datacamp / CDN77 (VPN-uri)
        31898, // Oracle Cloud
        45102, 37963, // Alibaba
        132203, 45090, // Tencent
        36352, // ColoCrossing
        53667, // FranTech / BuyVM
        46606, // Unified Layer
        26496, // GoDaddy
        8100, // QuadraNet
        202425, // IP Volume
        49981, // WorldStream
        197540, // netcup
        8560, // IONOS
        12876, // Scaleway
        62240, // Clouvider
        44477, // Stark Industries
        210644, // Aeza
        396356, // Latitude.sh
        62567, 13213, // DigitalOcean / UK2
        19318, // Interserver
        30633, // Leaseweb US
        40021, // Contabo US
        136787, // TEFINCOM (NordVPN)
        147049, // PacketHub (VPN)
        206092, // SEFLOW (VPN)
        43357, // Owl Limited (VPN)
        200651, // Flokinet
        51852, // Private Layer
    };

    private static readonly string[] DatacenterWords =
    {
        "hosting", "host ", "cloud", "server", "datacenter", "data center", "data-center", "vps", "colocation",
        "dedicated", "vpn", "proxy", "virtual", "digitalocean", "linode", "hetzner", "ovh", "amazon", "google llc",
        "microsoft corporation", "oracle", "alibaba", "tencent", "contabo", "vultr", "leaseweb", "m247", "datacamp",
    };

    private readonly ILogger _logger;
    private readonly object _lock = new();
    private MaxMindDatabase? _asn;
    private string? _path;
    private DateTime _loadedWrite;
    private DateTimeOffset _lastCheck;

    public NetworkClassifier(ILogger logger)
    {
        _logger = logger;
    }

    public bool HasDatabase => _asn != null;

    public string? DatabasePath => _path;

    /// <summary>Calea bazei: cea din setari, altfel cea a plugin-ului Jurnal de acces.</summary>
    public static string DefaultPath(string pluginsDataRoot) =>
        Path.Combine(pluginsDataRoot, "AccessLog", "geoip", "GeoLite2-ASN.mmdb");

    public NetworkInfo Classify(IPAddress? ip, string? cloudflareCountry, string configuredPath, string fallbackPath)
    {
        if (string.Equals(cloudflareCountry, "T1", StringComparison.OrdinalIgnoreCase))
        {
            return new NetworkInfo(null, "Tor", NetworkInfo.Tor);
        }

        if (ip == null)
        {
            return NetworkInfo.None;
        }

        ip = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        if (IsLocal(ip))
        {
            return new NetworkInfo(null, null, NetworkInfo.Local);
        }

        var database = Database(string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath.Trim());
        Dictionary<string, object?>? record;
        try
        {
            record = database?.Find(ip);
        }
        catch (InvalidDataException)
        {
            record = null;
        }

        if (record == null)
        {
            return NetworkInfo.None;
        }

        long? asn = record.TryGetValue("autonomous_system_number", out var number) && number != null ? Convert.ToInt64(number) : null;
        var organization = record.TryGetValue("autonomous_system_organization", out var org) ? org as string : null;
        return new NetworkInfo(asn, organization, Kind(asn, organization));
    }

    internal static string Kind(long? asn, string? organization)
    {
        if (asn == 13335)
        {
            return NetworkInfo.Warp;
        }

        if (asn != null && DatacenterAsns.Contains(asn.Value))
        {
            return NetworkInfo.Datacenter;
        }

        var name = " " + (organization ?? string.Empty).ToLowerInvariant() + " ";
        return DatacenterWords.Any(w => name.Contains(w, StringComparison.Ordinal)) ? NetworkInfo.Datacenter : NetworkInfo.Residential;
    }

    internal static bool IsLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var b = ip.GetAddressBytes();
        if (b.Length == 4)
        {
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC;
    }

    /// <summary>Baza se reincarca singura cand plugin-ul Jurnal de acces o actualizeaza (verificare la 10 minute).</summary>
    private MaxMindDatabase? Database(string path)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (_asn != null && path == _path && now - _lastCheck < TimeSpan.FromMinutes(10))
            {
                return _asn;
            }

            _lastCheck = now;
            try
            {
                if (!File.Exists(path))
                {
                    if (_path != path || _asn != null)
                    {
                        _logger.Info("Inregistrare: baza ASN lipseste ({0}); reteaua vizitatorilor nu se poate clasifica", path);
                    }

                    _asn = null;
                    _path = path;
                    return null;
                }

                var written = File.GetLastWriteTimeUtc(path);
                if (_asn == null || path != _path || written != _loadedWrite)
                {
                    _asn = MaxMindDatabase.Open(path);
                    _path = path;
                    _loadedWrite = written;
                    _logger.Info("Inregistrare: baza ASN incarcata din {0}", path);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _logger.ErrorException("Inregistrare: baza ASN nu a putut fi citita", ex);
                _asn = null;
            }

            return _asn;
        }
    }
}
