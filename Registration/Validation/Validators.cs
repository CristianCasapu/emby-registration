using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Registration.Validation;

/// <summary>
/// Regulile formularului. Pagina publica le repeta pentru mesaje pe loc, dar sursa de
/// adevar e aici: orice cerere trece prin ele pe server. Rezultatul e un cod de eroare
/// (ex. „username_taken”), tradus de pagina.
/// </summary>
public static partial class Validators
{
    /// <summary>Nume interzise intotdeauna, pe langa cele din configuratie.</summary>
    private static readonly string[] BuiltInReserved =
    {
        "admin", "administrator", "administrador", "root", "system", "sysadmin", "emby", "embyserver",
        "server", "support", "helpdesk", "help", "info", "contact", "owner", "moderator", "mod",
        "staff", "security", "abuse", "postmaster", "hostmaster", "webmaster", "noreply", "no-reply",
        "api", "www", "mail", "guest", "anonymous", "null", "undefined", "user", "users", "test",
        "operator", "superuser", "service", "daemon", "nobody", "headend", "registration",
    };

    /// <summary>Fragmente care nu pot aparea nicaieri in nume (se pot da drept administratori).</summary>
    private static readonly string[] ReservedFragments = { "admin", "emby", "moderator", "support" };

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?$")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"[._-]{2,}")]
    private static partial Regex RepeatedSeparators();

    [GeneratedRegex(@"^[\p{L}\p{M}](?:[\p{L}\p{M}' .\-]*[\p{L}\p{M}.])?$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$")]
    private static partial Regex EmailPattern();

    /// <summary>Username-ul ca cheie de comparatie: NFKC, litere mici, fara spatii la capete.</summary>
    public static string NormalizeUsername(string? value) =>
        (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();

    public static string? Username(string normalized, int minLength, int maxLength, IEnumerable<string> extraReserved)
    {
        if (normalized.Length < minLength || normalized.Length > maxLength)
        {
            return "username_length";
        }

        if (!UsernamePattern().IsMatch(normalized) || RepeatedSeparators().IsMatch(normalized))
        {
            return "username_chars";
        }

        var compact = normalized.Replace(".", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        if (BuiltInReserved.Contains(normalized) || BuiltInReserved.Contains(compact)
            || ReservedFragments.Any(f => compact.Contains(f, StringComparison.Ordinal))
            || extraReserved.Any(r => string.Equals(NormalizeUsername(r), normalized, StringComparison.Ordinal)))
        {
            return "username_reserved";
        }

        return null;
    }

    /// <summary>Prenume sau nume: litere (orice alfabet), spatiu, cratima, apostrof, punct.</summary>
    public static string NormalizeName(string? value)
    {
        var text = (value ?? string.Empty).Normalize(NormalizationForm.FormC).Replace('’', '\'');
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string? Name(string normalized) =>
        normalized.Length is < 1 or > 50 || !NamePattern().IsMatch(normalized) ? "name_invalid" : null;

    public static string NormalizeEmail(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static string? Email(string normalized)
    {
        if (normalized.Length > 254 || !EmailPattern().IsMatch(normalized))
        {
            return "email_invalid";
        }

        var local = normalized[..normalized.IndexOf('@')];
        return local.Length > 64 ? "email_invalid" : null;
    }

    public static string EmailDomain(string normalizedEmail) => normalizedEmail[(normalizedEmail.IndexOf('@') + 1)..];

    public static string? Password(string? password, int minLength, string normalizedUsername)
    {
        if (string.IsNullOrEmpty(password) || password.Length < minLength)
        {
            return "password_short";
        }

        if (password.Length > 128)
        {
            return "password_long";
        }

        if (password.Distinct().Count() < 4)
        {
            return "password_weak";
        }

        var lower = password.ToLowerInvariant();
        if (normalizedUsername.Length >= 3 && lower.Contains(normalizedUsername, StringComparison.Ordinal))
        {
            return "password_username";
        }

        return null;
    }

    /// <summary>PIN-ul de profil Emby: exact 4 cifre, fara combinatii evidente.</summary>
    public static string? Pin(string? pin)
    {
        if (pin == null || pin.Length != 4 || !pin.All(char.IsAsciiDigit))
        {
            return "pin_invalid";
        }

        var ascending = "0123456789012";
        var descending = "9876543210987";
        if (pin.Distinct().Count() == 1 || ascending.Contains(pin, StringComparison.Ordinal)
            || descending.Contains(pin, StringComparison.Ordinal))
        {
            return "pin_weak";
        }

        return null;
    }

    /// <summary>Liste din configuratie: o valoare pe linie (sau separate prin virgula), fara comentarii.</summary>
    public static IEnumerable<string> Lines(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith('#'));

    /// <summary>Numele afisat intr-un mesaj: prenume + initiala numelui.</summary>
    public static string ShortName(string firstName, string lastName) =>
        string.IsNullOrEmpty(lastName) ? firstName : $"{firstName} {char.ToUpper(lastName[0], CultureInfo.InvariantCulture)}.";
}
