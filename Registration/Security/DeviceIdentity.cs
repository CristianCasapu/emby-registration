using System.Security.Cryptography;
using System.Text;

namespace Registration.Security;

/// <summary>
/// Identificatorul unui browser: 16 octeti aleatori + semnatura HMAC, emis la prima vizita
/// si pastrat in cookie si in localStorage. Semnatura impiedica pe cineva sa inventeze
/// identificatorul altui dispozitiv (ca sa-l blocheze); stergerea lui e acoperita de
/// celelalte semnale (adresa, amprenta, conturile Emby din browser).
/// </summary>
public sealed class DeviceIdentity
{
    public const string CookieName = "emby_registration_device";

    private readonly Func<byte[]> _secret;

    public DeviceIdentity(Func<byte[]> secret)
    {
        _secret = secret;
    }

    public string Issue()
    {
        var id = RandomNumberGenerator.GetBytes(16);
        return FormToken.Base64Url(id) + "." + FormToken.Base64Url(Sign(id));
    }

    /// <summary>Id-ul (fara semnatura) daca e valid, altfel null.</summary>
    public string? Verify(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 100)
        {
            return null;
        }

        var dot = value.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        try
        {
            var id = FormToken.FromBase64Url(value[..dot]);
            var signature = FormToken.FromBase64Url(value[(dot + 1)..]);
            return id.Length == 16 && CryptographicOperations.FixedTimeEquals(signature, Sign(id)) ? value[..dot] : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Ce se pastreaza in cerere: un hash, nu identificatorul (nu poate fi refolosit).</summary>
    public string Hash(string id) =>
        Convert.ToHexString(HMACSHA256.HashData(_secret(), Encoding.UTF8.GetBytes("device:" + id))).ToLowerInvariant()[..32];

    private byte[] Sign(byte[] id) => HMACSHA256.HashData(_secret(), id.Concat(Encoding.UTF8.GetBytes("device")).ToArray())[..16];

    /// <summary>Valoarea cookie-ului din antetul Cookie.</summary>
    public static string? FromCookieHeader(string? header)
    {
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        foreach (var part in header.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq > 0 && part[..eq].Trim() == CookieName)
            {
                return part[(eq + 1)..].Trim();
            }
        }

        return null;
    }
}
