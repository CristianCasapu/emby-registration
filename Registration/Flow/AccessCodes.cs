using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Registration.Notifications;
using Registration.Security;
using Registration.Storage;

namespace Registration.Flow;

public sealed class UnlockResult
{
    public bool Ok { get; set; }

    /// <summary>Codul de acces semnat (cookie), la succes.</summary>
    public string? Pass { get; set; }

    public int AttemptsLeft { get; set; }

    public DateTimeOffset? BlockedUntil { get; set; }

    /// <summary>wrong_code, blocked, rate_limited sau motivul de cont dublu (duplicate_device, duplicate_network, signed_in).</summary>
    public string? Error { get; set; }

    /// <summary>La signed_in: contul cu care browserul e conectat.</summary>
    public string? Detail { get; set; }
}

/// <summary>
/// Codul de acces: formularul se deschide doar cu codul dat de admin. Un singur cod e
/// valabil; dupa folosire se genereaza automat altul, iar un cod emis o data nu mai apare
/// niciodata. Codurile gresite blocheaza separat adresa IP si dispozitivul.
/// </summary>
public sealed partial class RegistrationManager
{
    /// <summary>Litere mari si cifre fara perechi care se confunda (0/O/Q/D, 1/I/L, 2/Z, 5/S, 8/B, 6/G, U/V).</summary>
    public const string CodeAlphabet = "ACEFHJKMNPRTWXY34679";

    public const int CodeLength = 5;

    public const string PassCookieName = "emby_registration_pass";

    /// <summary>Codul curent; il genereaza daca lipseste.</summary>
    public (string Code, DateTimeOffset Created) CurrentCode(DateTimeOffset now) => Store.Write(d =>
    {
        if (string.IsNullOrEmpty(d.AccessCode))
        {
            NewCode(d, now);
        }

        return (d.AccessCode!, d.AccessCodeCreated ?? now);
    });

    /// <summary>Adminul inlocuieste codul (de exemplu, l-a dat cuiva care nu l-a mai folosit).</summary>
    public string ReplaceCode(DateTimeOffset now) => Store.Write(d =>
    {
        var old = d.CodeHistory.FirstOrDefault(c => c.Code == d.AccessCode);
        if (old != null && old.UsedAt == null)
        {
            old.Replaced = true;
        }

        return NewCode(d, now);
    });

    private static string NewCode(StoreData data, DateTimeOffset now)
    {
        var used = data.CodeHistory.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);
        string code;
        do
        {
            code = new string(Enumerable.Range(0, CodeLength).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());
        }
        while (used.Contains(code));

        data.AccessCode = code;
        data.AccessCodeCreated = now;
        data.CodeHistory.Add(new CodeUse { Code = code, CreatedAt = now });
        return code;
    }

    internal static string NormalizeCode(string? code) =>
        new string((code ?? string.Empty).ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    /// <summary>Blocarea activa pentru adresa sau dispozitivul vizitatorului, daca exista.</summary>
    public AccessBlock? ActiveBlock(string? ip, string? deviceHash, DateTimeOffset now) => Store.Read(d =>
        d.Blocks.Where(b => b.IsActive(now) && ((b.Kind == "ip" && ip != null && b.Value == ip) || (b.Kind == "device" && deviceHash != null && b.Value == deviceHash)))
            .OrderByDescending(b => b.Until)
            .FirstOrDefault());

    public string? DeviceHashOf(string? device) => Devices.Verify(device) is { } id ? Devices.Hash(id) : null;

    public UnlockResult TryUnlock(string? code, Origin origin, string? device, DateTimeOffset now)
        => TryUnlock(code, origin, device, now, Array.Empty<string>(), null);

    public UnlockResult TryUnlock(string? code, Origin origin, string? device, DateTimeOffset now, string[] embyUsers, Registration.Network.NetworkInfo? network)
    {
        var settings = Settings;
        var ip = RateLimiter.Normalize(origin.Ip)?.ToString() ?? "-";
        var deviceHash = DeviceHashOf(device);

        var rateKey = "unlock:" + RateLimiter.IpKey(origin.Ip);
        if (!Limits.Allows(rateKey, 10, TimeSpan.FromMinutes(10), now))
        {
            return new UnlockResult { Error = "rate_limited" };
        }

        Limits.Record(rateKey, now);
        if (ActiveBlock(ip, deviceHash, now) is { } block)
        {
            return new UnlockResult { Error = "blocked", BlockedUntil = block.Until };
        }

        var entered = NormalizeCode(code);
        var current = CurrentCode(now).Code;
        var correct = entered.Length == CodeLength
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entered), Encoding.ASCII.GetBytes(current));

        if (correct)
        {
            // Codul corect nu se consuma pentru cineva care oricum nu poate cere cont (are deja unul).
            var duplicate = FindDuplicates(new DeviceEvidence { Device = device, EmbyUsers = embyUsers }, origin, network ?? Network(origin), null, now)
                .FirstOrDefault(x => x.Action == DuplicateActions.Block);
            if (duplicate != null)
            {
                Store.Count("dup_" + duplicate.Code, now);
                return new UnlockResult
                {
                    Error = BlockReason(duplicate),
                    Detail = duplicate.Code == "signed_in" ? duplicate.Detail[(duplicate.Detail.LastIndexOf(' ') + 1)..] : null,
                };
            }

            Store.Write(d =>
            {
                var use = d.CodeHistory.FirstOrDefault(c => c.Code == current);
                if (use != null)
                {
                    use.UsedAt = now;
                    use.UsedByIp = ip;
                    use.UsedByCountry = origin.IpCountry;
                }

                // Folosit o data: codul se schimba imediat.
                NewCode(d, now);
                RequestStore.Increment(d, "code_used", now);
            });

            Notifier.Notify(NotifyEvents.NewRequest, "Înregistrare: cod de acces folosit",
                $"Codul {current} a deschis formularul pentru {ip}{(origin.IpCountry == null ? string.Empty : " / " + origin.IpCountry)}. Codul nou se vede în pagina plugin-ului.");
            _logger.Info("Inregistrare: codul de acces a fost folosit de {0}; s-a generat altul", ip);
            return new UnlockResult { Ok = true, Pass = IssuePass(deviceHash ?? "ip:" + ip, now.AddMinutes(Math.Max(5, settings.CodeUnlockMinutes))) };
        }

        var max = Math.Max(1, settings.MaxCodeAttempts);
        var window = TimeSpan.FromHours(Math.Max(1, settings.CodeBlockHours));
        var label = $"{ip}{(origin.IpCountry == null ? string.Empty : " / " + origin.IpCountry)}{(origin.UserAgent == null ? string.Empty : ", " + ShortAgent(origin.UserAgent))}";

        var (ipCount, deviceCount, newBlocks) = Store.Write(d =>
        {
            d.CodeAttempts.RemoveAll(a => now - a.At > window);
            d.CodeAttempts.Add(new CodeAttempt { At = now, Ip = ip, DeviceHash = deviceHash });
            RequestStore.Increment(d, "code_wrong", now);

            var byIp = d.CodeAttempts.Count(a => a.Ip == ip);
            var byDevice = deviceHash == null ? 0 : d.CodeAttempts.Count(a => a.DeviceHash == deviceHash);
            var blocks = new List<AccessBlock>();
            if (byIp >= max && !d.Blocks.Any(b => b.Kind == "ip" && b.Value == ip && b.IsActive(now)))
            {
                blocks.Add(new AccessBlock { Kind = "ip", Value = ip, Attempts = byIp });
            }

            if (deviceHash != null && byDevice >= max && !d.Blocks.Any(b => b.Kind == "device" && b.Value == deviceHash && b.IsActive(now)))
            {
                blocks.Add(new AccessBlock { Kind = "device", Value = deviceHash, Attempts = byDevice });
            }

            foreach (var b in blocks)
            {
                b.Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
                b.Label = label;
                b.CreatedAt = now;
                b.Until = now + window;
                d.Blocks.Add(b);
                RequestStore.Increment(d, "code_block_" + b.Kind, now);
            }

            return (byIp, byDevice, blocks);
        });

        _logger.Info("Inregistrare: cod de acces gresit de la {0} ({1}/{2})", ip, Math.Max(ipCount, deviceCount), max);
        if (newBlocks.Count > 0)
        {
            Notifier.Notify(NotifyEvents.Abuse, "Înregistrare: blocat după coduri greșite",
                $"{label}: {string.Join(" și ", newBlocks.Select(b => b.Kind == "ip" ? "adresa IP" : "dispozitivul"))} blocat(e) {window.TotalHours:0} ore. Deblocare din pagina plugin-ului.", warning: true);
            return new UnlockResult { Error = "blocked", BlockedUntil = now + window };
        }

        return new UnlockResult { Error = "wrong_code", AttemptsLeft = Math.Max(0, max - Math.Max(ipCount, deviceCount)) };
    }

    /// <summary>Deblocheaza o blocare (si sterge incercarile gresite ale adresei/dispozitivului).</summary>
    public bool Unblock(string id, DateTimeOffset now) => Store.Write(d =>
    {
        var block = d.Blocks.FirstOrDefault(b => b.Id == id);
        if (block == null)
        {
            return false;
        }

        block.Until = now;
        d.CodeAttempts.RemoveAll(a => (block.Kind == "ip" && a.Ip == block.Value) || (block.Kind == "device" && a.DeviceHash == block.Value));
        return true;
    });

    /// <summary>Formularul e deblocat pentru acest vizitator (cod corect introdus recent)?</summary>
    public bool HasPass(string? pass, string? device, string? ip, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(pass) || pass.Length > 300)
        {
            return false;
        }

        var dot = pass.IndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        try
        {
            var payload = FormToken.FromBase64Url(pass[..dot]);
            var signature = FormToken.FromBase64Url(pass[(dot + 1)..]);
            if (!CryptographicOperations.FixedTimeEquals(signature, PassSignature(payload)) || payload.Length < 9)
            {
                return false;
            }

            var until = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64BigEndian(payload));
            var subject = Encoding.UTF8.GetString(payload, 8, payload.Length - 8);
            var deviceHash = DeviceHashOf(device);
            return until > now && (subject == deviceHash || subject == "ip:" + ip);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string IssuePass(string subject, DateTimeOffset until)
    {
        var text = Encoding.UTF8.GetBytes(subject);
        var payload = new byte[8 + text.Length];
        BinaryPrimitives.WriteInt64BigEndian(payload, until.ToUnixTimeSeconds());
        text.CopyTo(payload, 8);
        return FormToken.Base64Url(payload) + "." + FormToken.Base64Url(PassSignature(payload));
    }

    private static byte[] PassSignature(byte[] payload) =>
        HMACSHA256.HashData(Convert.FromBase64String(Settings.FormSecret), Encoding.UTF8.GetBytes("pass:").Concat(payload).ToArray());

    /// <summary>Curata incercarile vechi si blocarile expirate de peste o saptamana.</summary>
    public void MaintainCodes(DateTimeOffset now)
    {
        var window = TimeSpan.FromHours(Math.Max(1, Settings.CodeBlockHours));
        Store.Write(d =>
        {
            d.CodeAttempts.RemoveAll(a => now - a.At > window);
            d.Blocks.RemoveAll(b => now - b.Until > TimeSpan.FromDays(7));
        });
    }

    internal static string ShortAgent(string ua)
    {
        var os = ua.Contains("Android") ? "Android" : ua.Contains("iPhone") || ua.Contains("iPad") ? "iOS" : ua.Contains("Windows") ? "Windows"
            : ua.Contains("Mac OS") ? "macOS" : ua.Contains("Linux") ? "Linux" : string.Empty;
        var browser = ua.Contains("Edg/") ? "Edge" : ua.Contains("Firefox/") ? "Firefox" : ua.Contains("Chrome/") ? "Chrome" : ua.Contains("Safari/") ? "Safari" : string.Empty;
        var text = string.Join(" / ", new[] { browser, os }.Where(x => x.Length > 0));
        return text.Length > 0 ? text : ua[..Math.Min(ua.Length, 40)];
    }
}
