using System.Net;
using System.Security.Cryptography;
using Registration.Flow;
using Registration.Network;
using Registration.Security;

namespace Registration.Tests;

public class DeviceIdentityTests
{
    [Fact]
    public void IssuedIdsVerifyAndForgeriesFail()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var devices = new DeviceIdentity(() => secret);
        var value = devices.Issue();
        var id = devices.Verify(value);
        Assert.NotNull(id);
        Assert.Equal(32, devices.Hash(id!).Length);
        Assert.Equal(devices.Hash(id!), devices.Hash(id!));

        Assert.Null(devices.Verify(id + ".AAAAAAAAAAAAAAAAAAAAAA"));
        Assert.Null(new DeviceIdentity(() => RandomNumberGenerator.GetBytes(32)).Verify(value));
        Assert.Null(devices.Verify("gunoi"));
        Assert.Null(devices.Verify(null));
    }

    [Fact]
    public void CookieParsing()
    {
        Assert.Equal("abc.def", DeviceIdentity.FromCookieHeader("x=1; emby_registration_device=abc.def; y=2"));
        Assert.Null(DeviceIdentity.FromCookieHeader("x=1"));
        Assert.Null(DeviceIdentity.FromCookieHeader(null));
    }
}

public class SameOriginTests
{
    private static Origin From(string? origin, string? host, string? site) =>
        new(IPAddress.Loopback, null, null) { OriginHeader = origin, Host = host, FetchSite = site };

    [Theory]
    [InlineData("https://exemplu.ro:2096", "exemplu.ro:2096", "same-origin", true)]
    [InlineData("https://exemplu.ro:2096", "exemplu.ro", null, true)]
    [InlineData("http://127.0.0.1:8096", "127.0.0.1:8096", "same-origin", true)]
    [InlineData("https://rau.ro", "exemplu.ro:2096", "cross-site", false)]
    [InlineData("https://exemplu.ro:2096", "exemplu.ro:2096", "cross-site", false)]
    [InlineData(null, "exemplu.ro:2096", null, false)]
    [InlineData("null", "exemplu.ro:2096", null, false)]
    public void Origins(string? origin, string? host, string? site, bool expected)
    {
        Assert.Equal(expected, RegistrationManager.SameOrigin(From(origin, host, site)));
    }
}

public class NetworkTests
{
    [Theory]
    [InlineData(16509L, "AMAZON-02", NetworkInfo.Datacenter)]
    [InlineData(9009L, "M247 Europe SRL", NetworkInfo.Datacenter)]
    [InlineData(99999L, "Some Cloud Hosting Ltd", NetworkInfo.Datacenter)]
    [InlineData(13335L, "CLOUDFLARENET", NetworkInfo.Warp)]
    [InlineData(8708L, "RCS & RDS SA", NetworkInfo.Residential)]
    [InlineData(12302L, "Vodafone Romania S.A.", NetworkInfo.Residential)]
    [InlineData(6830L, "Liberty Global B.V.", NetworkInfo.Residential)]
    public void Kinds(long asn, string org, string expected)
    {
        Assert.Equal(expected, NetworkClassifier.Kind(asn, org));
    }

    [Theory]
    [InlineData("172.16.0.5", true)]
    [InlineData("192.168.1.2", true)]
    [InlineData("100.70.1.2", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("82.78.61.200", false)]
    [InlineData("2a02:2f0e::1", false)]
    public void LocalAddresses(string ip, bool expected)
    {
        Assert.Equal(expected, NetworkClassifier.IsLocal(IPAddress.Parse(ip)));
    }

    [Fact]
    public void TorFromCloudflareCountry()
    {
        var classifier = new NetworkClassifier(new NullLogger());
        Assert.Equal(NetworkInfo.Tor, classifier.Classify(IPAddress.Parse("1.2.3.4"), "T1", "", "/nu/exista").Kind);
        Assert.Equal(NetworkInfo.Unknown, classifier.Classify(IPAddress.Parse("1.2.3.4"), "RO", "", "/nu/exista").Kind);
    }

    [Fact]
    public void Fingerprints()
    {
        Assert.Equal("abcdef0123456789", RegistrationManager.CleanFingerprint("ABCDEF0123456789"));
        Assert.Null(RegistrationManager.CleanFingerprint("xyz"));
        Assert.Null(RegistrationManager.CleanFingerprint("<script>alert(1)</script>"));
    }

    private sealed class NullLogger : MediaBrowser.Model.Logging.ILogger
    {
        public void Info(string message, params object[] paramList) { }
        public void Error(string message, params object[] paramList) { }
        public void Warn(string message, params object[] paramList) { }
        public void Debug(string message, params object[] paramList) { }
        public void Fatal(string message, params object[] paramList) { }
        public void FatalException(string message, Exception exception, params object[] paramList) { }
        public void ErrorException(string message, Exception exception, params object[] paramList) { }
        public void LogMultiline(string message, MediaBrowser.Model.Logging.LogSeverity severity, System.Text.StringBuilder additionalContent) { }
        public void Log(MediaBrowser.Model.Logging.LogSeverity severity, string message, params object[] paramList) { }
        public void Info(ReadOnlyMemory<char> message) { }
        public void Error(ReadOnlyMemory<char> message) { }
        public void Warn(ReadOnlyMemory<char> message) { }
        public void Debug(ReadOnlyMemory<char> message) { }
        public void Log(MediaBrowser.Model.Logging.LogSeverity severity, ReadOnlyMemory<char> message) { }
    }
}

public class PublicMessageTests
{
    [Theory]
    [InlineData("closed", "closed")]
    [InlineData("busy", "closed")]
    [InlineData("full", "closed")]
    [InlineData("not_yet", "not_yet")]
    [InlineData("duplicate_network", "unavailable")]
    [InlineData("signed_in", "unavailable")]
    [InlineData("network_blocked", "unavailable")]
    [InlineData("blocked", "unavailable")]
    [InlineData("rate_limited", "unavailable")]
    public void ReasonsAreGeneric(string reason, string expected)
    {
        Assert.Equal(expected, Registration.Api.PublicService.PublicReason(reason));
    }

    [Fact]
    public void ResultsHideTheCheckThatFailed()
    {
        var bot = Registration.Api.PublicService.PublicResult(SubmitResult.Fail("Invalid", "bot_check"));
        Assert.Equal(("Error", "error"), (bot.Outcome, bot.Error));
        var fast = Registration.Api.PublicService.PublicResult(SubmitResult.Fail("Invalid", "too_fast"));
        Assert.Equal("error", fast.Error);
        var limited = Registration.Api.PublicService.PublicResult(new SubmitResult { Outcome = "RateLimited", Error = "rate_limited", RetryAfterSeconds = 60 });
        Assert.Equal(("Closed", "unavailable", 0), (limited.Outcome, limited.Error, limited.RetryAfterSeconds));
        var fields = Registration.Api.PublicService.PublicResult(new SubmitResult { Outcome = "Invalid", Error = "fields", Fields = { ["phone"] = "phone_taken", ["email"] = "email_blocked" } });
        Assert.Equal("phone_rejected", fields.Fields["phone"]);
        Assert.Equal("email_rejected", fields.Fields["email"]);
    }
}
