namespace Registration.Validation;

public sealed record PhoneCountry(string Iso, string Dial, string Name, string NameEn, int MinDigits, int MaxDigits, string Example);

/// <summary>
/// Numere de telefon in format international E.164 (+ prefix tara + numar national, fara 0).
/// Tabel mic, fara libphonenumber: prefixul si lungimea numarului national pentru tarile
/// uzuale; pentru restul se verifica doar regula E.164 (8–15 cifre).
/// </summary>
public static class PhoneNumbers
{
    public static readonly IReadOnlyList<PhoneCountry> Countries = new PhoneCountry[]
    {
        new("RO", "40", "România", "Romania", 9, 9, "712345678"),
        new("MD", "373", "Republica Moldova", "Moldova", 8, 8, "62112345"),
        new("AT", "43", "Austria", "Austria", 7, 13, "6641234567"),
        new("BE", "32", "Belgia", "Belgium", 8, 9, "470123456"),
        new("BG", "359", "Bulgaria", "Bulgaria", 8, 9, "881234567"),
        new("CA", "1", "Canada", "Canada", 10, 10, "5062345678"),
        new("CH", "41", "Elveția", "Switzerland", 9, 9, "781234567"),
        new("CY", "357", "Cipru", "Cyprus", 8, 8, "96123456"),
        new("CZ", "420", "Cehia", "Czechia", 9, 9, "601123456"),
        new("DE", "49", "Germania", "Germany", 10, 11, "15123456789"),
        new("DK", "45", "Danemarca", "Denmark", 8, 8, "32123456"),
        new("EE", "372", "Estonia", "Estonia", 7, 8, "51234567"),
        new("ES", "34", "Spania", "Spain", 9, 9, "612345678"),
        new("FI", "358", "Finlanda", "Finland", 6, 10, "412345678"),
        new("FR", "33", "Franța", "France", 9, 9, "612345678"),
        new("GB", "44", "Regatul Unit", "United Kingdom", 10, 10, "7400123456"),
        new("GR", "30", "Grecia", "Greece", 10, 10, "6912345678"),
        new("HR", "385", "Croația", "Croatia", 8, 9, "921234567"),
        new("HU", "36", "Ungaria", "Hungary", 8, 9, "201234567"),
        new("IE", "353", "Irlanda", "Ireland", 9, 9, "850123456"),
        new("IL", "972", "Israel", "Israel", 9, 9, "502345678"),
        new("IT", "39", "Italia", "Italy", 9, 11, "3123456789"),
        new("LT", "370", "Lituania", "Lithuania", 8, 8, "61234567"),
        new("LU", "352", "Luxemburg", "Luxembourg", 9, 9, "628123456"),
        new("LV", "371", "Letonia", "Latvia", 8, 8, "21234567"),
        new("MT", "356", "Malta", "Malta", 8, 8, "96961234"),
        new("NL", "31", "Țările de Jos", "Netherlands", 9, 9, "612345678"),
        new("NO", "47", "Norvegia", "Norway", 8, 8, "40612345"),
        new("PL", "48", "Polonia", "Poland", 9, 9, "512345678"),
        new("PT", "351", "Portugalia", "Portugal", 9, 9, "912345678"),
        new("RS", "381", "Serbia", "Serbia", 8, 9, "601234567"),
        new("SE", "46", "Suedia", "Sweden", 7, 9, "701234567"),
        new("SI", "386", "Slovenia", "Slovenia", 8, 8, "31234567"),
        new("SK", "421", "Slovacia", "Slovakia", 9, 9, "912123456"),
        new("TR", "90", "Turcia", "Türkiye", 10, 10, "5012345678"),
        new("UA", "380", "Ucraina", "Ukraine", 9, 9, "501234567"),
        new("US", "1", "Statele Unite", "United States", 10, 10, "2015550123"),
        new("AE", "971", "Emiratele Arabe Unite", "United Arab Emirates", 8, 9, "501234567"),
        new("AU", "61", "Australia", "Australia", 9, 9, "412345678"),
    };

    public static PhoneCountry? ByIso(string? iso) =>
        Countries.FirstOrDefault(c => string.Equals(c.Iso, iso, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Aduce numarul la forma E.164. Accepta spatii, cratime, puncte, paranteze si prefixul
    /// „00”; un numar fara prefix international primeste prefixul tarii alese (fara 0-ul de
    /// inceput, ex. 0712 345 678 cu RO → +40712345678).
    /// </summary>
    public static string? Normalize(string? input, string? countryIso)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var raw = input.Trim();
        var international = raw.StartsWith('+') || raw.StartsWith("00", StringComparison.Ordinal);
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (raw.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '.' or '(' or ')' or '+' or '/')))
        {
            return null;
        }

        if (international)
        {
            if (raw.StartsWith("00", StringComparison.Ordinal))
            {
                digits = digits[2..];
            }
        }
        else
        {
            var country = ByIso(countryIso);
            if (country == null)
            {
                return null;
            }

            digits = country.Dial + digits.TrimStart('0');
        }

        return "+" + digits;
    }

    /// <summary>Tara dupa prefix (cel mai lung prefix care se potriveste); null daca nu e in tabel.</summary>
    public static PhoneCountry? CountryOf(string e164)
    {
        var digits = e164.TrimStart('+');
        return Countries
            .Where(c => digits.StartsWith(c.Dial, StringComparison.Ordinal))
            .OrderByDescending(c => c.Dial.Length)
            .ThenBy(c => c.Iso == "US" ? 0 : 1)
            .FirstOrDefault();
    }

    /// <summary>Null daca numarul e valid, altfel codul erorii.</summary>
    public static string? Validate(string? e164, IReadOnlyCollection<string> allowedIsos)
    {
        if (e164 == null || e164.Length < 9 || e164.Length > 16 || e164[0] != '+' || e164[1] == '0'
            || !e164.Skip(1).All(char.IsAsciiDigit))
        {
            return "phone_invalid";
        }

        var country = CountryOf(e164);
        if (country != null)
        {
            var national = e164.Length - 1 - country.Dial.Length;
            if (national < country.MinDigits || national > country.MaxDigits)
            {
                return "phone_invalid";
            }
        }

        if (allowedIsos.Count > 0)
        {
            // +1 e comun pentru SUA si Canada: oricare dintre ele permisa ajunge.
            var matches = Countries.Where(c => e164.TrimStart('+').StartsWith(c.Dial, StringComparison.Ordinal));
            if (!matches.Any(c => allowedIsos.Contains(c.Iso, StringComparer.OrdinalIgnoreCase)))
            {
                return "phone_country";
            }
        }

        return null;
    }
}
