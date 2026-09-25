using Registration.Validation;

namespace Registration.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("cristi", null)]
    [InlineData("ana.maria", null)]
    [InlineData("user_01", null)]
    [InlineData("ab", "username_length")]
    [InlineData(".ana", "username_chars")]
    [InlineData("ana.", "username_chars")]
    [InlineData("ana..maria", "username_chars")]
    [InlineData("ana maria", "username_chars")]
    [InlineData("ștefan", "username_chars")]
    [InlineData("admin", "username_reserved")]
    [InlineData("super.admin2", "username_reserved")]
    [InlineData("e-m-b-y", "username_reserved")]
    [InlineData("root", "username_reserved")]
    public void Username(string input, string? expected)
    {
        var normalized = Validators.NormalizeUsername(input);
        Assert.Equal(expected, Validators.Username(normalized, 3, 32, Array.Empty<string>()));
    }

    [Fact]
    public void UsernameNormalizesCaseAndCompatibilityForms()
    {
        Assert.Equal("cristi", Validators.NormalizeUsername("  CRISTI "));
        // Litere „fullwidth” se reduc la ASCII, deci nu pot imita un nume existent.
        Assert.Equal("admin", Validators.NormalizeUsername("ａｄｍｉｎ"));
    }

    [Fact]
    public void ExtraReservedNames()
    {
        Assert.Equal("username_reserved", Validators.Username("familie", 3, 32, new[] { "Familie" }));
    }

    [Theory]
    [InlineData("Ana", null)]
    [InlineData("Ana-Maria", null)]
    [InlineData("O'Neil", null)]
    [InlineData("Ștefănescu", null)]
    [InlineData("Jean Pierre", null)]
    [InlineData("Ana2", "name_invalid")]
    [InlineData("<script>", "name_invalid")]
    [InlineData("", "name_invalid")]
    [InlineData("-Ana", "name_invalid")]
    public void Names(string input, string? expected)
    {
        Assert.Equal(expected, Validators.Name(Validators.NormalizeName(input)));
    }

    [Fact]
    public void NameCollapsesSpacesAndApostrophes()
    {
        Assert.Equal("Jean Pierre", Validators.NormalizeName("  Jean   Pierre "));
        Assert.Equal("O'Neil", Validators.NormalizeName("O’Neil"));
    }

    [Theory]
    [InlineData("ana@gmail.com", null)]
    [InlineData("ana.maria+emby@example.co.uk", null)]
    [InlineData("ana@gmail", "email_invalid")]
    [InlineData("ana@@gmail.com", "email_invalid")]
    [InlineData("ana gmail.com", "email_invalid")]
    [InlineData("ana@-gmail.com", "email_invalid")]
    public void Emails(string input, string? expected)
    {
        Assert.Equal(expected, Validators.Email(Validators.NormalizeEmail(input)));
    }

    [Theory]
    [InlineData("corect-cal-baterie", "ana", null)]
    [InlineData("scurta", "ana", "password_short")]
    [InlineData("aaaaaaaaaaaa", "ana", "password_weak")]
    [InlineData("parola-lui-cristi", "cristi", "password_username")]
    public void Passwords(string password, string username, string? expected)
    {
        Assert.Equal(expected, Validators.Password(password, 10, username));
    }

    [Theory]
    [InlineData("4827", null)]
    [InlineData("123", "pin_invalid")]
    [InlineData("12a4", "pin_invalid")]
    [InlineData("1111", "pin_weak")]
    [InlineData("1234", "pin_weak")]
    [InlineData("9876", "pin_weak")]
    [InlineData("8901", "pin_weak")]
    public void Pins(string pin, string? expected)
    {
        Assert.Equal(expected, Validators.Pin(pin));
    }

    [Fact]
    public void LinesSkipCommentsAndBlanks()
    {
        Assert.Equal(new[] { "a.ro", "b.ro", "c.ro" }, Validators.Lines("a.ro\n# comentariu\n\n b.ro ,c.ro"));
    }
}

public class PhoneTests
{
    [Theory]
    [InlineData("0712 345 678", "RO", "+40712345678")]
    [InlineData("712345678", "RO", "+40712345678")]
    [InlineData("+40 712-345-678", "DE", "+40712345678")]
    [InlineData("0040712345678", "RO", "+40712345678")]
    [InlineData("(0151) 2345 6789", "DE", "+4915123456789")]
    [InlineData("0712abc", "RO", null)]
    public void Normalize(string input, string country, string? expected)
    {
        Assert.Equal(expected, PhoneNumbers.Normalize(input, country));
    }

    [Theory]
    [InlineData("+40712345678", null)]
    [InlineData("+4071234567", "phone_invalid")]
    [InlineData("+407123456789", "phone_invalid")]
    [InlineData("+37362112345", null)]
    [InlineData("+12015550123", null)]
    [InlineData("+0712345678", "phone_invalid")]
    [InlineData("+86123456789012", null)] // tara necunoscuta in tabel: doar regula E.164
    [InlineData("+40", "phone_invalid")]
    public void Validate(string e164, string? expected)
    {
        Assert.Equal(expected, PhoneNumbers.Validate(e164, Array.Empty<string>()));
    }

    [Fact]
    public void AllowedCountries()
    {
        var allowed = new[] { "RO", "MD" };
        Assert.Null(PhoneNumbers.Validate("+40712345678", allowed));
        Assert.Null(PhoneNumbers.Validate("+37362112345", allowed));
        Assert.Equal("phone_country", PhoneNumbers.Validate("+4915123456789", allowed));
        Assert.Equal("phone_country", PhoneNumbers.Validate("+86123456789012", allowed));
        Assert.Null(PhoneNumbers.Validate("+15062345678", new[] { "CA" }));
    }

    [Fact]
    public void CountryByLongestPrefix()
    {
        Assert.Equal("MD", PhoneNumbers.CountryOf("+37362112345")!.Iso);
        Assert.Equal("RO", PhoneNumbers.CountryOf("+40712345678")!.Iso);
    }
}
