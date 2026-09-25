using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using Registration.Notifications;
using Registration.Security;

namespace Registration.Updates;

public sealed class UpdateStatus
{
    public string CurrentVersion { get; set; } = string.Empty;

    public string? LatestVersion { get; set; }

    public string? LatestTag { get; set; }

    public string? ReleaseNotes { get; set; }

    public string? ReleaseUrl { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool UpdateAvailable { get; set; }

    /// <summary>O versiune noua (sau cea anterioara) e pe disc; se incarca la repornirea Emby.</summary>
    public string? InstalledPendingRestart { get; set; }

    public string? BackupVersion { get; set; }

    public DateTimeOffset? LastCheck { get; set; }

    public string? Error { get; set; }

    public bool PendingRestart { get; set; }
}

/// <summary>
/// Actualizare din GitHub Releases. Emby nu accepta cataloage de plugin-uri externe, asa
/// ca plugin-ul isi descarca singur versiunea noua, verifica semnatura ECDSA P-256 cu
/// cheia publica inclusa in DLL (cheia privata nu e pe GitHub, deci un cont GitHub
/// compromis nu poate publica un DLL acceptat) si inlocuieste fisierul; noua versiune se
/// incarca la repornirea Emby. Versiunea anterioara ramane pentru revenire.
/// </summary>
public sealed class GitHubUpdater
{
    public const string AssetName = "Registration.dll";
    public const string SignatureName = "Registration.dll.sig";

    private readonly Plugin _plugin;
    private readonly IServerApplicationHost _appHost;
    private readonly AdminNotifier _notifier;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly UpdateStatus _status;
    private string? _etag;
    private List<Release>? _cached;
    private string? _notifiedVersion;

    public GitHubUpdater(Plugin plugin, IServerApplicationHost appHost, AdminNotifier notifier, ILogger logger)
    {
        _plugin = plugin;
        _appHost = appHost;
        _notifier = notifier;
        _logger = logger;
        _status = new UpdateStatus { CurrentVersion = Format(plugin.Version) };
        Directory.CreateDirectory(plugin.UpdatesDirectory);
        _status.BackupVersion = ReadBackupVersion();
    }

    private string BackupPath => Path.Combine(_plugin.UpdatesDirectory, "Registration.dll.bak");

    public UpdateStatus Status
    {
        get
        {
            _status.PendingRestart = _appHost.HasPendingRestart;
            _status.BackupVersion = ReadBackupVersion();
            return _status;
        }
    }

    public async Task AutoCheckAsync(DateTimeOffset now)
    {
        var settings = _plugin.Configuration;
        if (!settings.AutoCheckUpdates || (_status.LastCheck != null && now - _status.LastCheck < TimeSpan.FromHours(12)))
        {
            return;
        }

        await CheckAsync(CancellationToken.None).ConfigureAwait(false);
        if (_status.UpdateAvailable && _status.LatestVersion != _notifiedVersion)
        {
            _notifiedVersion = _status.LatestVersion;
            _notifier.Notify("Update", "Înregistrare: versiune nouă disponibilă",
                $"Versiunea {_status.LatestVersion} poate fi instalată din pagina plugin-ului (instalată: {_status.CurrentVersion}).");
        }
    }

    public async Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _status.LastCheck = DateTimeOffset.UtcNow;
            var release = await LatestAsync(cancellationToken).ConfigureAwait(false);
            _status.Error = null;
            _status.LatestVersion = release == null ? null : Format(release.Version);
            _status.LatestTag = release?.Tag;
            _status.ReleaseNotes = release?.Notes;
            _status.ReleaseUrl = release?.HtmlUrl;
            _status.PublishedAt = release?.PublishedAt;
            _status.UpdateAvailable = release != null && release.Version > _plugin.Version
                && _status.InstalledPendingRestart != Format(release.Version);
        }
        catch (Exception ex)
        {
            _status.Error = ex.Message;
            _logger.Warn("Inregistrare: verificarea actualizarilor a esuat: {0}", ex.Message);
        }
        finally
        {
            _lock.Release();
        }

        return Status;
    }

    /// <summary>Descarca, verifica si pune pe disc ultima versiune. Intoarce null la succes, altfel eroarea.</summary>
    public async Task<string?> InstallAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var release = await LatestAsync(cancellationToken).ConfigureAwait(false);
            if (release == null || release.Version <= _plugin.Version)
            {
                return "Nu există o versiune mai nouă.";
            }

            if (release.DllUrl == null || release.SignatureUrl == null)
            {
                return $"Release-ul {release.Tag} nu conține {AssetName} și {SignatureName}.";
            }

            var dll = await DownloadAsync(release.DllUrl, 20 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
            var signature = await DownloadAsync(release.SignatureUrl, 4096, cancellationToken).ConfigureAwait(false);
            var error = Verify(dll, signature, release.Version);
            if (error != null)
            {
                _logger.Error("Inregistrare: actualizarea {0} a fost refuzata: {1}", release.Tag, error);
                return error;
            }

            File.Copy(_plugin.AssemblyPath, BackupPath, overwrite: true);
            File.WriteAllText(BackupPath + ".version", Format(_plugin.Version));
            Replace(dll);

            _status.InstalledPendingRestart = Format(release.Version);
            _status.UpdateAvailable = false;
            _appHost.NotifyPendingRestart();
            _logger.Info("Inregistrare: versiunea {0} a fost instalata; se incarca la repornirea Emby", release.Tag);
            _notifier.Notify("Update", "Înregistrare: actualizare instalată", $"Versiunea {Format(release.Version)} se încarcă la repornirea Emby.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Inregistrare: actualizarea a esuat", ex);
            return ex.Message;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Pune inapoi versiunea dinaintea ultimei actualizari.</summary>
    public string? Rollback()
    {
        if (!File.Exists(BackupPath))
        {
            return "Nu există o versiune anterioară salvată.";
        }

        var version = ReadBackupVersion();
        Replace(File.ReadAllBytes(BackupPath));
        _status.InstalledPendingRestart = version;
        _appHost.NotifyPendingRestart();
        _logger.Info("Inregistrare: revenire la versiunea {0}", version ?? "?");
        return null;
    }

    private void Replace(byte[] dll)
    {
        // Fisier nou + rename: pe Linux DLL-ul incarcat ramane valid pana la repornire.
        var target = _plugin.AssemblyPath;
        var temp = target + ".new";
        File.WriteAllBytes(temp, dll);
        File.Move(temp, target, overwrite: true);
    }

    internal static string? Verify(byte[] dll, byte[] signature, Version expected) => Verify(dll, signature, expected, ReleaseKey());

    internal static string? Verify(byte[] dll, byte[] signature, Version expected, string publicKeyPem)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        if (!key.VerifyData(dll, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
        {
            return "Semnătura fișierului nu este validă.";
        }

        var temp = Path.Combine(Path.GetTempPath(), "registration-update-" + Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            File.WriteAllBytes(temp, dll);
            var name = AssemblyName.GetAssemblyName(temp);
            if (name.Name != "Registration")
            {
                return "Fișierul descărcat nu este plugin-ul Înregistrare.";
            }

            if (name.Version == null || Normalize(name.Version) != Normalize(expected))
            {
                return $"Versiunea din fișier ({name.Version}) nu corespunde release-ului ({expected}).";
            }
        }
        finally
        {
            File.Delete(temp);
        }

        return null;
    }

    internal static string ReleaseKey()
    {
        using var stream = typeof(GitHubUpdater).Assembly.GetManifestResourceStream("Registration.Updates.release-key.pem")
            ?? throw new InvalidOperationException("Cheia publică de verificare lipsește din plugin.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record Release(string Tag, Version Version, string? Notes, string? HtmlUrl, DateTimeOffset? PublishedAt, string? DllUrl, string? SignatureUrl);

    private async Task<Release?> LatestAsync(CancellationToken cancellationToken)
    {
        var repository = _plugin.Configuration.GitHubRepository.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
        {
            throw new InvalidOperationException("Depozitul GitHub trebuie să fie de forma cont/nume.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases?per_page=20");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (_etag != null && _cached != null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(_etag, _etag.StartsWith("W/", StringComparison.Ordinal)));
        }

        using var response = await WebChecks.Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        List<Release> releases;
        if (response.StatusCode == HttpStatusCode.NotModified && _cached != null)
        {
            releases = _cached;
        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Depozitul {repository} nu există sau nu este public.");
        }
        else
        {
            response.EnsureSuccessStatusCode();
            releases = Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            _cached = releases;
            _etag = response.Headers.ETag?.Tag;
        }

        return releases.OrderByDescending(r => r.Version).FirstOrDefault();
    }

    private List<Release> Parse(string json)
    {
        var list = new List<Release>();
        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.GetProperty("draft").GetBoolean()
                || (item.GetProperty("prerelease").GetBoolean() && !_plugin.Configuration.IncludePrereleases))
            {
                continue;
            }

            var tag = item.GetProperty("tag_name").GetString() ?? string.Empty;
            if (!Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out var version))
            {
                continue;
            }

            string? Asset(string name) => item.GetProperty("assets").EnumerateArray()
                .Where(a => a.GetProperty("name").GetString() == name)
                .Select(a => a.GetProperty("browser_download_url").GetString())
                .FirstOrDefault();

            list.Add(new Release(
                tag,
                Normalize(version),
                item.TryGetProperty("body", out var body) ? body.GetString() : null,
                item.TryGetProperty("html_url", out var url) ? url.GetString() : null,
                item.TryGetProperty("published_at", out var published) && published.ValueKind == JsonValueKind.String ? published.GetDateTimeOffset() : null,
                Asset(AssetName),
                Asset(SignatureName)));
        }

        return list;
    }

    private static async Task<byte[]> DownloadAsync(string url, int maxBytes, CancellationToken cancellationToken)
    {
        if (!url.StartsWith("https://github.com/", StringComparison.Ordinal) && !url.StartsWith("https://objects.githubusercontent.com/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Adresa de descărcare nu este de pe GitHub.");
        }

        using var response = await WebChecks.Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
            {
                throw new InvalidOperationException("Fișierul descărcat este prea mare.");
            }
        }

        return buffer.ToArray();
    }

    private string? ReadBackupVersion()
    {
        var file = BackupPath + ".version";
        return File.Exists(BackupPath) && File.Exists(file) ? File.ReadAllText(file).Trim() : null;
    }

    internal static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

    private static string Format(Version v) => v.Revision > 0 ? v.ToString(4) : v.ToString(3);
}
