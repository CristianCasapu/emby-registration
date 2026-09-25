using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Registration.Security;

/// <summary>
/// Tokenul emis odata cu formularul: ora emiterii, un nonce, dificultatea proof-of-work
/// si hash-ul adresei IP, semnate HMAC-SHA256. Fara el nu se poate trimite o cerere
/// direct la API; la fel, nu poate fi folosit de pe alta adresa, dupa expirare sau a
/// doua oara.
/// </summary>
public sealed class FormToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    private const int PayloadLength = 8 + 16 + 1 + 8;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _used = new();

    public FormToken(Func<byte[]> secret)
    {
        Secret = secret;
    }

    private Func<byte[]> Secret { get; }

    public string Issue(IPAddress? ip, int powBits, DateTimeOffset now)
    {
        var payload = new byte[PayloadLength];
        BinaryPrimitives.WriteInt64BigEndian(payload, now.ToUnixTimeSeconds());
        RandomNumberGenerator.Fill(payload.AsSpan(8, 16));
        payload[24] = (byte)Math.Clamp(powBits, 0, 32);
        IpHash(ip).CopyTo(payload.AsSpan(25, 8));
        return Base64Url(payload) + "." + Base64Url(Sign(payload));
    }

    /// <summary>Verifica tokenul; la succes intoarce ora emiterii si dificultatea ceruta.</summary>
    public TokenCheck Verify(string? token, IPAddress? ip, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200)
        {
            return TokenCheck.Fail("token_invalid");
        }

        var dot = token.IndexOf('.');
        byte[] payload, signature;
        try
        {
            payload = FromBase64Url(token[..dot]);
            signature = FromBase64Url(token[(dot + 1)..]);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            return TokenCheck.Fail("token_invalid");
        }

        if (payload.Length != PayloadLength || !CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
        {
            return TokenCheck.Fail("token_invalid");
        }

        var issued = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64BigEndian(payload));
        if (now - issued > Lifetime || issued - now > TimeSpan.FromMinutes(1))
        {
            return TokenCheck.Fail("token_expired");
        }

        if (!CryptographicOperations.FixedTimeEquals(payload.AsSpan(25, 8), IpHash(ip)))
        {
            return TokenCheck.Fail("token_expired");
        }

        return new TokenCheck(null, issued, payload[24]);
    }

    /// <summary>Marcheaza tokenul ca folosit; false daca fusese deja folosit.</summary>
    public bool Consume(string token, DateTimeOffset now)
    {
        foreach (var old in _used.Where(p => now - p.Value > Lifetime).Select(p => p.Key).ToList())
        {
            _used.TryRemove(old, out _);
        }

        return _used.TryAdd(token, now);
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(Secret(), payload);

    private byte[] IpHash(IPAddress? ip)
    {
        var text = ip == null ? "-" : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
        return HMACSHA256.HashData(Secret(), Encoding.UTF8.GetBytes("ip:" + text)).AsSpan(0, 8).ToArray();
    }

    internal static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4));
    }
}

public sealed record TokenCheck(string? Error, DateTimeOffset Issued, int PowBits)
{
    public static TokenCheck Fail(string error) => new(error, default, 0);
}
