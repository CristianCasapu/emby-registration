using System.Security.Cryptography;
using MediaBrowser.Model.Users;
using Registration.Flow;
using Registration.Storage;
using Registration.Updates;

namespace Registration.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "registration-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void PersistsAndReloads()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new RequestStore(_dir);
        store.Write(d => d.Requests.Add(new RegistrationRecord { Id = "a1", Username = "ana", Email = "ana@example.com", CreatedAt = now }));
        store.Count("requests", now);

        var reloaded = new RequestStore(_dir);
        Assert.Equal("ana", reloaded.Read(d => d.Requests.Single().Username));
        Assert.Equal(1, reloaded.Read(d => d.Stats.Single().Counters["requests"]));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_dir, "registrations.json")));
        }
    }

    [Fact]
    public void FailedWriteRollsBack()
    {
        var store = new RequestStore(_dir);
        store.Write(d => d.Requests.Add(new RegistrationRecord { Id = "a1" }));
        Assert.Throws<InvalidOperationException>(() => store.Write(d =>
        {
            d.Requests.Clear();
            throw new InvalidOperationException();
        }));
        Assert.Equal(1, store.Read(d => d.Requests.Count));
    }

    [Fact]
    public void CorruptFileIsKeptAside()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "registrations.json"), "{ nu e json");
        var store = new RequestStore(_dir);
        Assert.Equal(0, store.Read(d => d.Requests.Count));
        Assert.Single(Directory.GetFiles(_dir, "registrations.json.corrupt-*"));
    }

    [Fact]
    public void InviteUsability()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(new InviteCode { MaxUses = 1, Uses = 0 }.IsUsable(now));
        Assert.False(new InviteCode { MaxUses = 1, Uses = 1 }.IsUsable(now));
        Assert.True(new InviteCode { MaxUses = 0, Uses = 50 }.IsUsable(now));
        Assert.False(new InviteCode { ExpiresAt = now.AddMinutes(-1) }.IsUsable(now));
        Assert.False(new InviteCode { Revoked = true }.IsUsable(now));
    }
}

public class PolicyTests
{
    [Fact]
    public void NewAccountHasOnlyMinimalRights()
    {
        var settings = new PluginConfiguration { Libraries = new[] { "a0bbc61d122b40889e2b24e91fddcf4d" }, StreamLimit = 2, RemoteBitrateLimitMbps = 8 };
        var policy = AccessPolicy.Build(settings, disabled: true);

        Assert.True(policy.IsDisabled);
        Assert.False(policy.IsAdministrator);
        Assert.True(policy.IsHidden && policy.IsHiddenRemotely && policy.IsHiddenFromUnusedDevices);
        Assert.False(policy.EnableContentDownloading || policy.EnableSyncTranscoding || policy.EnableMediaConversion);
        Assert.False(policy.EnablePublicSharing || policy.AllowSharingPersonalItems || policy.AllowCameraUpload);
        Assert.False(policy.EnableContentDeletion || policy.EnableSubtitleManagement || policy.EnableLiveTvManagement);
        Assert.False(policy.EnableRemoteControlOfOtherUsers || policy.EnableSharedDeviceControl);
        Assert.False(policy.EnableAllFolders);
        Assert.Equal(new[] { "a0bbc61d122b40889e2b24e91fddcf4d" }, policy.EnabledFolders);
        Assert.False(policy.EnableLiveTvAccess);
        Assert.Equal(2, policy.SimultaneousStreamLimit);
        Assert.Equal(8_000_000, policy.RemoteClientBitrateLimit);
        Assert.True(policy.EnableMediaPlayback && policy.EnableUserPreferenceAccess);
    }

    [Fact]
    public void AllLibraries()
    {
        var policy = AccessPolicy.Build(new PluginConfiguration { AllLibraries = true, Libraries = new[] { "x" } }, disabled: false);
        Assert.True(policy.EnableAllFolders);
        Assert.Empty(policy.EnabledFolders);
        Assert.False(policy.IsDisabled);
    }

    [Fact]
    public void ForbiddenRightsAreWithdrawn()
    {
        var policy = new UserPolicy { IsAdministrator = true, EnableContentDownloading = true, EnableContentDeletionFromFolders = new[] { "x" }, EnableLiveTvAccess = true };
        Assert.True(AccessPolicy.ApplyForbidden(policy));
        Assert.False(policy.IsAdministrator);
        Assert.False(policy.EnableContentDownloading);
        Assert.Empty(policy.EnableContentDeletionFromFolders);
        Assert.True(policy.EnableLiveTvAccess); // nu e interzis: il decide adminul
        Assert.False(AccessPolicy.ApplyForbidden(policy));
    }
}

public class UpdaterTests
{
    [Fact]
    public void AcceptsOnlyCorrectlySignedPluginOfTheExpectedVersion()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicPem = key.ExportSubjectPublicKeyInfoPem();
        var dll = File.ReadAllBytes(typeof(Plugin).Assembly.Location);
        var version = typeof(Plugin).Assembly.GetName().Version!;
        var signature = key.SignData(dll, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        Assert.Null(GitHubUpdater.Verify(dll, signature, version, publicPem));
        Assert.NotNull(GitHubUpdater.Verify(dll, signature, new Version(99, 0, 0), publicPem));

        var tampered = (byte[])dll.Clone();
        tampered[^1] ^= 0xFF;
        Assert.Equal("Semnătura fișierului nu este validă.", GitHubUpdater.Verify(tampered, signature, version, publicPem));

        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Equal("Semnătura fișierului nu este validă.", GitHubUpdater.Verify(dll, signature, version, other.ExportSubjectPublicKeyInfoPem()));
    }

    [Fact]
    public void EmbeddedReleaseKeyLoads()
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(GitHubUpdater.ReleaseKey());
        Assert.Equal(256, key.KeySize);
    }
}
