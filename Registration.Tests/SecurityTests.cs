using System.Net;
using System.Security.Cryptography;
using Registration.Security;

namespace Registration.Tests;

public class FormTokenTests
{
    private static readonly byte[] Secret = RandomNumberGenerator.GetBytes(32);
    private static readonly IPAddress Ip = IPAddress.Parse("203.0.113.7");
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidTokenCarriesIssueTimeAndDifficulty()
    {
        var tokens = new FormToken(() => Secret);
        var token = tokens.Issue(Ip, 18, Now);
        var check = tokens.Verify(token, Ip, Now.AddSeconds(30));
        Assert.Null(check.Error);
        Assert.Equal(Now, check.Issued);
        Assert.Equal(18, check.PowBits);
    }

    [Fact]
    public void MappedIpv4IsTheSameAddress()
    {
        var tokens = new FormToken(() => Secret);
        var token = tokens.Issue(Ip, 0, Now);
        Assert.Null(tokens.Verify(token, Ip.MapToIPv6(), Now).Error);
    }

    [Fact]
    public void RejectsOtherAddressExpiryTamperingAndOtherSecret()
    {
        var tokens = new FormToken(() => Secret);
        var token = tokens.Issue(Ip, 18, Now);
        Assert.NotNull(tokens.Verify(token, IPAddress.Parse("198.51.100.1"), Now).Error);
        Assert.Equal("token_expired", tokens.Verify(token, Ip, Now + FormToken.Lifetime + TimeSpan.FromSeconds(1)).Error);

        // Dificultatea e in payload: schimbarea ei strica semnatura.
        var payload = FormToken.FromBase64Url(token[..token.IndexOf('.')]);
        payload[24] = 1;
        var forged = FormToken.Base64Url(payload) + token[token.IndexOf('.')..];
        Assert.Equal("token_invalid", tokens.Verify(forged, Ip, Now).Error);

        var other = new FormToken(() => RandomNumberGenerator.GetBytes(32));
        Assert.Equal("token_invalid", other.Verify(token, Ip, Now).Error);
        Assert.Equal("token_invalid", tokens.Verify("gunoi", Ip, Now).Error);
        Assert.Equal("token_invalid", tokens.Verify(null, Ip, Now).Error);
    }

    [Fact]
    public void TokenCanBeConsumedOnce()
    {
        var tokens = new FormToken(() => Secret);
        var token = tokens.Issue(Ip, 0, Now);
        Assert.True(tokens.Consume(token, Now));
        Assert.False(tokens.Consume(token, Now.AddSeconds(5)));
    }
}

public class ProofOfWorkTests
{
    [Fact]
    public void SolutionIsVerified()
    {
        var token = "abc.def";
        var solution = ProofOfWork.Solve(token, 12);
        Assert.True(ProofOfWork.Verify(token, solution, 12));
        Assert.False(ProofOfWork.Verify(token + "x", solution, 12) && ProofOfWork.Verify(token + "x", solution, 20));
        Assert.False(ProofOfWork.Verify(token, null, 12));
        Assert.False(ProofOfWork.Verify(token, "12a", 12));
        Assert.True(ProofOfWork.Verify(token, null, 0));
    }

    [Fact]
    public void MatchesTheBrowserWorker()
    {
        // Aceeasi valoare gasita de Web/pow.js (verificata si cu crypto din Node).
        Assert.Equal("1917", ProofOfWork.Solve("abc.def", 12));
    }

    [Fact]
    public void LeadingZeroBits()
    {
        Assert.Equal(0, ProofOfWork.LeadingZeroBits(new byte[] { 0x80, 0 }));
        Assert.Equal(9, ProofOfWork.LeadingZeroBits(new byte[] { 0, 0x40 }));
        Assert.Equal(16, ProofOfWork.LeadingZeroBits(new byte[] { 0, 0 }));
    }
}

public class RateLimiterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SlidingWindow()
    {
        var limiter = new RateLimiter();
        var hour = TimeSpan.FromHours(1);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(limiter.Allows("k", 3, hour, Now.AddMinutes(i)));
            limiter.Record("k", Now.AddMinutes(i));
        }

        Assert.False(limiter.Allows("k", 3, hour, Now.AddMinutes(10)));
        Assert.Equal(TimeSpan.FromMinutes(50), limiter.RetryAfter("k", hour, Now.AddMinutes(10)));
        Assert.True(limiter.Allows("k", 3, hour, Now.AddMinutes(61)));
        Assert.True(limiter.Allows("k", 0, hour, Now));
    }

    [Fact]
    public void SubnetKeys()
    {
        Assert.Equal("net:203.0.113.0/24", RateLimiter.SubnetKey(IPAddress.Parse("203.0.113.77")));
        Assert.Equal("net:203.0.113.0/24", RateLimiter.SubnetKey(IPAddress.Parse("::ffff:203.0.113.5")));
        Assert.Equal("net:2001:db8:1:2::/64", RateLimiter.SubnetKey(IPAddress.Parse("2001:db8:1:2:aaaa::1")));
    }

    [Fact]
    public void CleanupDropsIdleKeys()
    {
        var limiter = new RateLimiter();
        limiter.Record("k", Now);
        limiter.Cleanup(TimeSpan.FromHours(1), Now.AddHours(2));
        Assert.Equal(0, limiter.Count("k", TimeSpan.FromDays(1), Now.AddHours(2)));
    }
}

public class IpListTests
{
    [Fact]
    public void AddressesAndNetworks()
    {
        var list = new IpList(new[] { "203.0.113.7", "198.51.100.0/24", "2001:db8::/32", "gunoi", "10.0.0.0/99" });
        Assert.True(list.Contains(IPAddress.Parse("203.0.113.7")));
        Assert.False(list.Contains(IPAddress.Parse("203.0.113.8")));
        Assert.True(list.Contains(IPAddress.Parse("198.51.100.200")));
        Assert.True(list.Contains(IPAddress.Parse("::ffff:198.51.100.1")));
        Assert.True(list.Contains(IPAddress.Parse("2001:db8:ffff::1")));
        Assert.False(list.Contains(IPAddress.Parse("2001:db9::1")));
        Assert.False(list.Contains(IPAddress.Parse("10.0.0.1")));
        Assert.False(list.Contains(null));
    }
}

public class WebCheckTests
{
    [Fact]
    public void PwnedResponseParsing()
    {
        var body = "0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n00D4F6E8FA6EECAD2A3AA415EEC418D38EC:2\r\n011053FD0102E94D6AE2F8B83D76FAF94F6:0\r\n";
        Assert.Equal(2, WebChecks.PwnedCount(body, "00D4F6E8FA6EECAD2A3AA415EEC418D38EC"));
        Assert.Equal(0, WebChecks.PwnedCount(body, "011053FD0102E94D6AE2F8B83D76FAF94F6"));
        Assert.Equal(0, WebChecks.PwnedCount(body, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));
    }

    [Fact]
    public void DisposableDomains()
    {
        Assert.True(DisposableEmail.IsDisposable("mailinator.com"));
        Assert.True(DisposableEmail.IsDisposable("sub.yopmail.com"));
        Assert.False(DisposableEmail.IsDisposable("gmail.com"));
    }
}
