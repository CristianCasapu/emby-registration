using System.Net;
using Registration.Flow;
using Registration.Notifications;
using Registration.Security;
using Registration.Storage;

namespace Registration.Tests;

public sealed class AccessCodeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "registration-code-" + Guid.NewGuid().ToString("N"));
    private readonly RegistrationManager _manager;
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    public AccessCodeTests()
    {
        var logger = new TestLogger();
        var store = new RequestStore(_dir);
        _manager = new RegistrationManager(null!, logger, store, new AdminNotifier(null!, logger), new WebChecks(logger));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Origin From(string ip) => new(IPAddress.Parse(ip), "RO", "Mozilla/5.0 (Linux; Android 14) Chrome/120");

    [Fact]
    public void AlphabetHasNoLookAlikes()
    {
        foreach (var c in "0O1IL2Z5S8BQDGUV")
        {
            Assert.DoesNotContain(c, RegistrationManager.CodeAlphabet);
        }

        Assert.Equal(RegistrationManager.CodeAlphabet.Length, RegistrationManager.CodeAlphabet.Distinct().Count());
    }

    [Fact]
    public void CodeIsStableUntilUsedThenRotatesAndNeverRepeats()
    {
        var first = _manager.CurrentCode(Now).Code;
        Assert.Matches("^[ACEFHJKMNPRTWXY34679]{5}$", first);
        Assert.Equal(first, _manager.CurrentCode(Now.AddMinutes(5)).Code);

        var device = _manager.Devices.Issue();
        var ok = _manager.TryUnlock(" " + first.ToLowerInvariant() + " ", From("203.0.113.10"), device, Now);
        Assert.True(ok.Ok);
        Assert.True(_manager.HasPass(ok.Pass, device, "203.0.113.10", Now.AddMinutes(10)));
        Assert.False(_manager.HasPass(ok.Pass, _manager.Devices.Issue(), "198.51.100.1", Now.AddMinutes(10)));
        Assert.False(_manager.HasPass(ok.Pass, device, "203.0.113.10", Now.AddHours(3)));

        var second = _manager.CurrentCode(Now).Code;
        Assert.NotEqual(first, second);
        Assert.False(_manager.TryUnlock(first, From("203.0.113.11"), _manager.Devices.Issue(), Now).Ok);

        // Multe rotiri: niciun cod repetat.
        var seen = new HashSet<string> { first, second };
        for (var i = 0; i < 300; i++)
        {
            Assert.True(seen.Add(_manager.ReplaceCode(Now)));
        }
    }

    [Fact]
    public void TwoWrongCodesBlockIpAndDeviceSeparately()
    {
        var code = _manager.CurrentCode(Now).Code;
        var device = _manager.Devices.Issue();
        var wrong = code == "AAAAA" ? "CCCCC" : "AAAAA";

        var first = _manager.TryUnlock(wrong, From("203.0.113.20"), device, Now);
        Assert.Equal("wrong_code", first.Error);
        Assert.Equal(1, first.AttemptsLeft);

        var second = _manager.TryUnlock(wrong, From("203.0.113.20"), device, Now.AddMinutes(1));
        Assert.Equal("blocked", second.Error);
        Assert.Equal(Now.AddMinutes(1).AddHours(24), second.BlockedUntil);

        var blocks = _manager.Store.Read(d => d.Blocks.ToList());
        Assert.Equal(new[] { "device", "ip" }, blocks.Select(b => b.Kind).OrderBy(k => k));

        // Chiar cu codul corect: blocat. Alt dispozitiv de pe aceeasi adresa: blocat (IP).
        Assert.Equal("blocked", _manager.TryUnlock(code, From("203.0.113.20"), device, Now.AddMinutes(2)).Error);
        Assert.Equal("blocked", _manager.TryUnlock(code, From("203.0.113.20"), _manager.Devices.Issue(), Now.AddMinutes(2)).Error);
        // Acelasi dispozitiv de pe alta adresa: blocat (dispozitiv).
        Assert.Equal("blocked", _manager.TryUnlock(code, From("198.51.100.20"), device, Now.AddMinutes(2)).Error);

        // Deblocarea adresei lasa dispozitivul blocat.
        Assert.True(_manager.Unblock(blocks.Single(b => b.Kind == "ip").Id, Now.AddMinutes(3)));
        Assert.True(_manager.TryUnlock(code, From("203.0.113.20"), _manager.Devices.Issue(), Now.AddMinutes(4)).Ok);
        Assert.Equal("blocked", _manager.TryUnlock(_manager.CurrentCode(Now).Code, From("198.51.100.21"), device, Now.AddMinutes(4)).Error);

        // Dupa 24 de ore, blocarea dispozitivului expira.
        Assert.True(_manager.TryUnlock(_manager.CurrentCode(Now).Code, From("198.51.100.22"), device, Now.AddHours(25)).Ok);
    }

    [Fact]
    public void NormalizesInput()
    {
        Assert.Equal("AC3F7", RegistrationManager.NormalizeCode(" ac3-f7 "));
    }

    private sealed class TestLogger : MediaBrowser.Model.Logging.ILogger
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
