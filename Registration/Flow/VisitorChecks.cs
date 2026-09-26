using System.Net;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Devices;
using Registration.Network;
using Registration.Security;
using Registration.Storage;
using Registration.Validation;

namespace Registration.Flow;

/// <summary>Un semn ca cererea vine de la cineva care are deja cont sau cerere.</summary>
public sealed record DuplicateSignal(string Code, string Action, string Detail);

/// <summary>Ce stie serverul despre browserul care trimite (pe langa adresa).</summary>
public sealed class DeviceEvidence
{
    /// <summary>Identificatorul semnat din cookie sau din localStorage.</summary>
    public string? Device { get; set; }

    /// <summary>Amprenta browserului (hash calculat in pagina).</summary>
    public string? Fingerprint { get; set; }

    /// <summary>Conturile Emby cu care browserul e conectat in interfata web (doar id-urile).</summary>
    public string[] EmbyUsers { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Poarta paginii (doar vizitatori legitimi) si regula „un cont per dispozitiv si per
/// adresa”. Adresele IP sunt dinamice, deci se compara doar pe o fereastra de timp; un
/// dispozitiv se recunoaste dupa identificatorul lui, dupa conturile Emby deja conectate in
/// browser si dupa amprenta, impreuna cu reteaua.
/// </summary>
public sealed partial class RegistrationManager
{
    private readonly object _devicesLock = new();
    private (DateTimeOffset At, List<DeviceInfo> Items) _devices = (DateTimeOffset.MinValue, new List<DeviceInfo>());

    public NetworkClassifier? Networks { get; set; }

    /// <summary>Id-ul serverului Emby (cheia din stocarea locala a interfetei web).</summary>
    public string? ServerId { get; set; }

    public IDeviceManager? DeviceManager { get; set; }

    public DeviceIdentity Devices { get; }

    /// <summary>Directorul comun al datelor plugin-urilor (pentru baza ASN a Jurnalului de acces).</summary>
    public string PluginsDataRoot { get; set; } = "/var/lib/emby/plugins";

    public NetworkInfo Network(Origin origin) =>
        Networks?.Classify(origin.Ip, origin.IpCountry, Settings.AsnDatabasePath, NetworkClassifier.DefaultPath(PluginsDataRoot)) ?? NetworkInfo.None;

    /// <summary>Null daca vizitatorul poate vedea formularul; altfel motivul (country_blocked, network_blocked).</summary>
    public string? GateVisitor(Origin origin, NetworkInfo network)
    {
        var settings = Settings;
        var countries = Validators.Lines(settings.AllowedVisitorCountries).ToList();
        if (countries.Count > 0 && network.Kind != NetworkInfo.Local
            && (origin.IpCountry == null || !countries.Contains(origin.IpCountry, StringComparer.OrdinalIgnoreCase)))
        {
            return "country_blocked";
        }

        if ((settings.BlockTor && network.Kind == NetworkInfo.Tor) || (settings.BlockDatacenters && network.Kind == NetworkInfo.Datacenter))
        {
            return "network_blocked";
        }

        return null;
    }

    /// <summary>Cererea POST vine din pagina servita de acest server (nu din alt site sau dintr-un script)?</summary>
    public static bool SameOrigin(Origin origin)
    {
        if (!string.IsNullOrEmpty(origin.FetchSite) && origin.FetchSite != "same-origin")
        {
            return false;
        }

        if (!Uri.TryCreate(origin.OriginHeader, UriKind.Absolute, out var from))
        {
            return false;
        }

        var host = origin.Host ?? string.Empty;
        var colon = host.LastIndexOf(':');
        if (colon > 0 && !host.EndsWith(']'))
        {
            host = host[..colon];
        }

        host = host.Trim('[', ']');
        if (string.Equals(from.Host, host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(Settings.PublicUrl, UriKind.Absolute, out var configured)
            && string.Equals(from.Host, configured.Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dificultatea proof-of-work: creste cand vin multe cereri in ultima ora.</summary>
    public int CurrentPowBits(DateTimeOffset now)
    {
        var bits = PowBits;
        if (bits == 0 || !Settings.AdaptiveProofOfWork)
        {
            return bits;
        }

        var recent = Limits.Count("global", TimeSpan.FromHours(1), now);
        return Math.Min(26, bits + (recent >= 15 ? 2 : recent >= 5 ? 1 : 0));
    }

    /// <summary>
    /// Semnele ca vizitatorul are deja cont sau cerere. <paramref name="phone"/> e null la
    /// incarcarea paginii (se verifica doar dispozitivul si adresa).
    /// </summary>
    public List<DuplicateSignal> FindDuplicates(DeviceEvidence evidence, Origin origin, NetworkInfo network, string? phone, DateTimeOffset now)
    {
        var settings = Settings;
        var signals = new List<DuplicateSignal>();
        var window = TimeSpan.FromDays(Math.Max(1, settings.DuplicateWindowDays));
        var ip = RateLimiter.Normalize(origin.Ip);
        var ipText = ip?.ToString();
        var subnet = RateLimiter.SubnetKey(ip);
        var local = network.Kind == NetworkInfo.Local || ip == null;

        // WARP: adresa e a Cloudflare, comuna pentru multi oameni; regulile pe adresa doar semnaleaza.
        string IpAction(string action) => network.Kind == NetworkInfo.Warp && action == DuplicateActions.Block ? DuplicateActions.Flag : action;

        void Add(string code, string action, string detail)
        {
            if (action is DuplicateActions.Block or DuplicateActions.Flag)
            {
                signals.Add(new DuplicateSignal(code, action, detail));
            }
        }

        var deviceId = Devices.Verify(evidence.Device);
        var deviceHash = deviceId == null ? null : Devices.Hash(deviceId);
        var records = Store.Read(d => d.Requests
            .Where(r => r.Status != RequestStatus.Deleted && (r.Status != RequestStatus.Expired || now - r.CreatedAt < TimeSpan.FromDays(1)))
            .Select(r => (r.Id, r.Username, r.Status, r.CreatedAt, r.Ip, r.Subnet, r.DeviceHash, r.Fingerprint, r.Phone))
            .ToList());

        foreach (var r in records)
        {
            if (deviceHash != null && r.DeviceHash == deviceHash)
            {
                Add("same_device", settings.SameDeviceAction, $"același dispozitiv ca cererea {r.Username} ({r.CreatedAt:dd.MM.yyyy})");
            }
        }

        // Conturile Emby deja conectate in acest browser (aceeasi origine => aceeasi stocare locala).
        foreach (var id in evidence.EmbyUsers.Take(20))
        {
            if (Guid.TryParse(id, out var guid) && _userManager.GetUserById(guid) is { } user)
            {
                Add("signed_in", settings.SameDeviceAction, $"browserul e deja conectat ca {user.Name}");
            }
        }

        if (!local)
        {
            foreach (var r in records.Where(r => now - r.CreatedAt < window))
            {
                if (r.Ip == ipText)
                {
                    Add("same_ip", IpAction(settings.SameIpAction), $"aceeași adresă IP ca cererea {r.Username} ({r.CreatedAt:dd.MM.yyyy})");
                }
                else if (r.Subnet == subnet)
                {
                    Add("same_subnet", IpAction(settings.SameSubnetAction), $"aceeași rețea ({subnet[4..]}) ca cererea {r.Username}");
                }

                if (!string.IsNullOrEmpty(evidence.Fingerprint) && r.Fingerprint == evidence.Fingerprint)
                {
                    if (r.Subnet == subnet)
                    {
                        Add("fingerprint_subnet", settings.FingerprintSubnetAction, $"același browser și aceeași rețea ca cererea {r.Username}");
                    }
                    else
                    {
                        Add("fingerprint", settings.FingerprintAction, $"browser identic cu al cererii {r.Username} (alt loc)");
                    }
                }
            }

            var recentDevices = TimeSpan.FromDays(Math.Max(1, settings.ExistingUserIpDays));
            foreach (var device in ExistingDevices(now).Where(d => now - d.DateLastActivity < recentDevices && d.IpAddress != null))
            {
                if (RateLimiter.Normalize(device.IpAddress)!.Equals(ip))
                {
                    Add("existing_ip", IpAction(settings.ExistingUserIpAction),
                        $"aceeași adresă IP ca un dispozitiv al contului {device.LastUserName} ({device.DateLastActivity:dd.MM.yyyy})");
                }
            }
        }

        if (phone != null)
        {
            foreach (var r in records.Where(r => r.Phone == phone))
            {
                Add("same_phone", settings.SamePhoneAction, $"același număr de telefon ca cererea {r.Username}");
            }
        }

        return signals
            .GroupBy(s => s.Code + "|" + s.Detail)
            .Select(g => g.First())
            .ToList();
    }

    private List<DeviceInfo> ExistingDevices(DateTimeOffset now)
    {
        if (DeviceManager == null)
        {
            return new List<DeviceInfo>();
        }

        lock (_devicesLock)
        {
            if (now - _devices.At > TimeSpan.FromMinutes(1))
            {
                try
                {
                    _devices = (now, DeviceManager.GetDevices(new DeviceQuery()).Items.ToList());
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Inregistrare: lista de dispozitive Emby nu a putut fi citita", ex);
                    _devices = (now, _devices.Items);
                }
            }

            return _devices.Items;
        }
    }

    /// <summary>Codul afisat vizitatorului pentru un semn de blocare.</summary>
    public static string BlockReason(DuplicateSignal signal) => signal.Code switch
    {
        "same_device" or "fingerprint_subnet" or "fingerprint" => "duplicate_device",
        "signed_in" => "signed_in",
        "same_phone" => "phone_taken",
        _ => "duplicate_network",
    };
}
