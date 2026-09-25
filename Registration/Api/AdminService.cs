using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Branding;
using MediaBrowser.Model.Services;
using Registration.Flow;
using Registration.Notifications;
using Registration.Storage;
using Registration.Updates;

namespace Registration.Api;

[Route("/Registration/Admin/Requests", "GET", Summary = "Cererile de cont si contoarele pe stari")]
[Authenticated(Roles = "admin")]
public sealed class GetRequests : IReturn<RequestList>
{
}

[Route("/Registration/Admin/Requests/Csv", "GET", Summary = "Cererile ca fisier CSV")]
[Authenticated(Roles = "admin")]
public sealed class GetRequestsCsv : IReturn<string>
{
}

[Route("/Registration/Admin/Approve", "POST", Summary = "Aproba una sau mai multe cereri")]
[Authenticated(Roles = "admin")]
public sealed class ApproveRequests : IReturn<ActionResult>
{
    public string[] Ids { get; set; } = Array.Empty<string>();
}

[Route("/Registration/Admin/Reject", "POST", Summary = "Respinge una sau mai multe cereri (contul dezactivat se sterge)")]
[Authenticated(Roles = "admin")]
public sealed class RejectRequests : IReturn<ActionResult>
{
    public string[] Ids { get; set; } = Array.Empty<string>();

    public string? Reason { get; set; }

    public bool NotifyUser { get; set; } = true;
}

[Route("/Registration/Admin/Delete", "POST", Summary = "Sterge cererea si datele ei, optional si contul Emby")]
[Authenticated(Roles = "admin")]
public sealed class DeleteRequest : IReturn<ActionResult>
{
    public string Id { get; set; } = string.Empty;

    public bool DeleteUser { get; set; }
}

[Route("/Registration/Admin/Managed", "POST", Summary = "Contul ramane sau iese de sub paznicul de politica")]
[Authenticated(Roles = "admin")]
public sealed class SetManaged : IReturn<ActionResult>
{
    public string Id { get; set; } = string.Empty;

    public bool Managed { get; set; }
}

[Route("/Registration/Admin/ResendConfirmation", "POST", Summary = "Retrimite e-mailul de confirmare")]
[Authenticated(Roles = "admin")]
public sealed class ResendConfirmation : IReturn<ActionResult>
{
    public string Id { get; set; } = string.Empty;
}

[Route("/Registration/Admin/Export", "GET", Summary = "Toate datele unei cereri (pentru o solicitare GDPR)")]
[Authenticated(Roles = "admin")]
public sealed class ExportRequest : IReturn<RegistrationRecord>
{
    public string Id { get; set; } = string.Empty;
}

[Route("/Registration/Admin/Invites", "GET", Summary = "Codurile de invitatie")]
[Authenticated(Roles = "admin")]
public sealed class GetInvites : IReturn<List<InviteCode>>
{
}

[Route("/Registration/Admin/Invites", "POST", Summary = "Genereaza un cod de invitatie")]
[Authenticated(Roles = "admin")]
public sealed class CreateInvite : IReturn<InviteCode>
{
    public string? Note { get; set; }

    public int MaxUses { get; set; } = 1;

    public int ValidDays { get; set; } = 14;
}

[Route("/Registration/Admin/Invites/Revoke", "POST", Summary = "Revoca un cod de invitatie")]
[Authenticated(Roles = "admin")]
public sealed class RevokeInvite : IReturn<ActionResult>
{
    public string Code { get; set; } = string.Empty;
}

[Route("/Registration/Admin/Stats", "GET", Summary = "Contoare pe zile")]
[Authenticated(Roles = "admin")]
public sealed class GetStats : IReturn<List<DailyStats>>
{
    public int Days { get; set; } = 30;
}

[Route("/Registration/Admin/Libraries", "GET", Summary = "Bibliotecile serverului, pentru alegerea accesului implicit")]
[Authenticated(Roles = "admin")]
public sealed class GetLibraries : IReturn<List<LibraryInfo>>
{
}

[Route("/Registration/Admin/TestEmail", "POST", Summary = "Trimite un e-mail de test cu setarile salvate")]
[Authenticated(Roles = "admin")]
public sealed class TestEmail : IReturn<ActionResult>
{
    public string To { get; set; } = string.Empty;
}

[Route("/Registration/Admin/TestTelegram", "POST", Summary = "Trimite un mesaj Telegram de test cu setarile salvate")]
[Authenticated(Roles = "admin")]
public sealed class TestTelegram : IReturn<ActionResult>
{
}

[Route("/Registration/Admin/LoginNotice", "POST", Summary = "Pune sau scoate textul despre inregistrare de pe ecranul de conectare Emby")]
[Authenticated(Roles = "admin")]
public sealed class SetLoginNotice : IReturn<ActionResult>
{
    /// <summary>Gol = scoate textul pus de plugin.</summary>
    public string? Text { get; set; }
}

[Route("/Registration/Admin/LoginNotice", "GET", Summary = "Textul pus de plugin pe ecranul de conectare")]
[Authenticated(Roles = "admin")]
public sealed class GetLoginNotice : IReturn<ActionResult>
{
}

[Route("/Registration/Admin/Update", "GET", Summary = "Versiunea instalata si ultima versiune de pe GitHub")]
[Authenticated(Roles = "admin")]
public sealed class GetUpdateStatus : IReturn<UpdateStatus>
{
    public bool Refresh { get; set; }
}

[Route("/Registration/Admin/Update/Install", "POST", Summary = "Instaleaza ultima versiune de pe GitHub")]
[Authenticated(Roles = "admin")]
public sealed class InstallUpdate : IReturn<ActionResult>
{
}

[Route("/Registration/Admin/Update/Rollback", "POST", Summary = "Revine la versiunea anterioara")]
[Authenticated(Roles = "admin")]
public sealed class RollbackUpdate : IReturn<ActionResult>
{
}

[Route("/Registration/Admin/Restart", "POST", Summary = "Reporneste Emby (refuza daca se reda ceva, fara Force)")]
[Authenticated(Roles = "admin")]
public sealed class RestartServer : IReturn<ActionResult>
{
    public bool Force { get; set; }
}

[Route("/Registration/Admin/Purge", "POST", Summary = "Sterge toate datele plugin-ului (cererile, invitatiile, statisticile)")]
[Authenticated(Roles = "admin")]
public sealed class PurgeData : IReturn<ActionResult>
{
    public string Confirm { get; set; } = string.Empty;
}

public sealed class RequestList
{
    public List<RequestView> Waiting { get; set; } = new();

    public List<RequestView> Processed { get; set; } = new();

    public Dictionary<string, int> Counts { get; set; } = new();
}

public sealed class RequestView
{
    public RegistrationRecord Record { get; set; } = new();

    /// <summary>Contul Emby mai exista.</summary>
    public bool UserExists { get; set; }

    public bool UserDisabled { get; set; }
}

public sealed class LibraryInfo
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? CollectionType { get; set; }
}

public sealed class ActionResult
{
    public bool Ok { get; set; } = true;

    public string? Error { get; set; }

    public int Done { get; set; }

    public List<string> Failed { get; set; } = new();

    public int ActivePlayback { get; set; }

    public string? Text { get; set; }
}

/// <summary>Rutele panoului de administrare (doar administratori).</summary>
public sealed class AdminService : IService, IRequiresRequest
{
    /// <summary>Marcajul textului pus de plugin pe ecranul de conectare, ca sa poata fi scos fara a atinge restul.</summary>
    private const string NoticeMarker = "⁣";

    private readonly IAuthorizationContext _authorization;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ISessionManager _sessionManager;
    private readonly IServerApplicationHost _appHost;
    private readonly IConfigurationManager _configurationManager;
    private readonly IHttpResultFactory _resultFactory;

    public AdminService(
        IAuthorizationContext authorization,
        ILibraryManager libraryManager,
        IUserManager userManager,
        ISessionManager sessionManager,
        IServerApplicationHost appHost,
        IConfigurationManager configurationManager,
        IHttpResultFactory resultFactory)
    {
        _authorization = authorization;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _sessionManager = sessionManager;
        _appHost = appHost;
        _configurationManager = configurationManager;
        _resultFactory = resultFactory;
    }

    public IRequest Request { get; set; } = null!;

    private static RegistrationManager Manager =>
        RegistrationHost.Manager ?? throw new InvalidOperationException("Plugin-ul Inregistrare nu a pornit.");

    private static GitHubUpdater Updater =>
        RegistrationHost.Updater ?? throw new InvalidOperationException("Plugin-ul Inregistrare nu a pornit.");

    private string Admin => _authorization.GetAuthorizationInfo(Request).User?.Name ?? "admin";

    public object Get(GetRequests request)
    {
        var records = Manager.Store.Read(d => d.Requests.Select(Clone).ToList());
        RequestView View(RegistrationRecord r)
        {
            var user = Manager.UserOf(r);
            return new RequestView
            {
                Record = r,
                UserExists = user != null,
                UserDisabled = user != null && _userManager.GetUserPolicy(user).IsDisabled,
            };
        }

        foreach (var r in records)
        {
            r.EmailTokenHash = null;
        }

        var list = new RequestList
        {
            Waiting = records.Where(r => RequestStatus.IsWaiting(r.Status)).OrderBy(r => r.CreatedAt).Select(View).ToList(),
            Processed = records.Where(r => !RequestStatus.IsWaiting(r.Status)).OrderByDescending(r => r.DecidedAt ?? r.CreatedAt).Select(View).ToList(),
        };

        foreach (var group in records.GroupBy(r => r.Status))
        {
            list.Counts[group.Key] = group.Count();
        }

        return list;
    }

    private static RegistrationRecord Clone(RegistrationRecord r) =>
        System.Text.Json.JsonSerializer.Deserialize<RegistrationRecord>(System.Text.Json.JsonSerializer.Serialize(r))!;

    public object Get(GetRequestsCsv request)
    {
        var records = Manager.Store.Read(d => d.Requests.OrderByDescending(r => r.CreatedAt).Select(Clone).ToList());
        var csv = new StringBuilder();
        csv.AppendLine("Id,Status,Username,FirstName,LastName,Email,Phone,Pin,Ip,IpCountry,CreatedAt,EmailConfirmedAt,DecidedAt,DecidedBy,Reason,InviteCode,Managed");
        foreach (var r in records)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                r.Id, r.Status, r.Username, r.FirstName, r.LastName, r.Email, r.Phone, r.HasPin ? "da" : "nu", r.Ip, r.IpCountry,
                Date(r.CreatedAt), Date(r.EmailConfirmedAt), Date(r.DecidedAt), r.DecidedBy, r.Reason, r.InviteCode, r.Managed ? "da" : "nu",
            }.Select(Csv)));
        }

        return _resultFactory.GetResult(Request, csv.ToString().AsSpan(), "text/csv; charset=utf-8", new Dictionary<string, string>
        {
            ["Content-Disposition"] = $"attachment; filename=\"inregistrari-{DateTime.UtcNow:yyyyMMdd}.csv\"",
            ["Cache-Control"] = "no-store",
        });
    }

    private static string Date(DateTimeOffset? d) => d?.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Camp CSV; valorile care incep cu = + - @ sunt prefixate, ca Excel sa nu le execute ca formule.</summary>
    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public object Post(ApproveRequests request)
    {
        var result = new ActionResult();
        foreach (var id in request.Ids.Distinct())
        {
            var error = Manager.Approve(id, Admin, DateTimeOffset.UtcNow);
            if (error == null)
            {
                result.Done++;
            }
            else
            {
                result.Failed.Add(id + ": " + error);
            }
        }

        result.Ok = result.Failed.Count == 0;
        return result;
    }

    public async Task<object> Post(RejectRequests request)
    {
        var result = new ActionResult();
        foreach (var id in request.Ids.Distinct())
        {
            var error = await Manager.RejectAsync(id, Admin, request.Reason, request.NotifyUser, DateTimeOffset.UtcNow).ConfigureAwait(false);
            if (error == null)
            {
                result.Done++;
            }
            else
            {
                result.Failed.Add(id + ": " + error);
            }
        }

        result.Ok = result.Failed.Count == 0;
        return result;
    }

    public async Task<object> Post(DeleteRequest request) => Result(await Manager.DeleteAsync(request.Id, request.DeleteUser).ConfigureAwait(false));

    public object Post(SetManaged request) => Result(Manager.SetManaged(request.Id, request.Managed));

    public object Post(ResendConfirmation request) => Result(Manager.ResendConfirmation(request.Id, DateTimeOffset.UtcNow));

    public object Get(ExportRequest request)
    {
        var record = Manager.Find(request.Id) ?? throw new FileNotFoundException("Cererea nu exista.");
        var copy = Clone(record);
        copy.EmailTokenHash = null;
        return _resultFactory.GetResult(Request, copy, new Dictionary<string, string>
        {
            ["Content-Disposition"] = $"attachment; filename=\"cerere-{copy.Username}.json\"",
            ["Cache-Control"] = "no-store",
        });
    }

    public object Get(GetInvites request) => Manager.Store.Read(d => d.Invites.OrderByDescending(i => i.CreatedAt).ToList());

    public object Post(CreateInvite request)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = string.Concat(Enumerable.Range(0, 10).Select(i => (i == 5 ? "-" : string.Empty) + alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]));
        var now = DateTimeOffset.UtcNow;
        var invite = new InviteCode
        {
            Code = code,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()[..Math.Min(request.Note.Trim().Length, 200)],
            CreatedAt = now,
            ExpiresAt = request.ValidDays > 0 ? now.AddDays(request.ValidDays) : null,
            MaxUses = Math.Max(0, request.MaxUses),
        };
        Manager.Store.Write(d => d.Invites.Add(invite));
        return invite;
    }

    public object Post(RevokeInvite request) => Result(Manager.Store.Write(d =>
    {
        var invite = d.Invites.FirstOrDefault(i => i.Code == request.Code);
        if (invite == null)
        {
            return "not_found";
        }

        invite.Revoked = true;
        return (string?)null;
    }));

    public object Get(GetStats request)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(request.Days, 1, 365)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Manager.Store.Read(d => d.Stats.Where(s => string.CompareOrdinal(s.Day, since) >= 0)
            .OrderBy(s => s.Day)
            .Select(s => new DailyStats { Day = s.Day, Counters = new Dictionary<string, int>(s.Counters) })
            .ToList());
    }

    public object Get(GetLibraries request) => _libraryManager.GetVirtualFolders()
        .Select(f => new LibraryInfo { Id = Guid.TryParse(f.Guid, out var g) ? g.ToString("N") : f.Guid, Name = f.Name, CollectionType = f.CollectionType })
        .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public async Task<object> Post(TestEmail request)
    {
        var settings = RegistrationManager.Settings;
        try
        {
            await Mailer.SendAsync(settings, new[] { request.To.Trim() }, "Înregistrare: e-mail de test",
                "Setările SMTP ale plugin-ului Înregistrare funcționează.", Request.CancellationToken).ConfigureAwait(false);
            return new ActionResult();
        }
        catch (Exception ex)
        {
            return new ActionResult { Ok = false, Error = ex.GetBaseException().Message };
        }
    }

    public async Task<object> Post(TestTelegram request)
    {
        try
        {
            await AdminNotifier.SendTelegramAsync(RegistrationManager.Settings, "Înregistrare: mesaj de test. Notificările Telegram funcționează.", Request.CancellationToken).ConfigureAwait(false);
            return new ActionResult();
        }
        catch (Exception ex)
        {
            return new ActionResult { Ok = false, Error = ex.GetBaseException().Message };
        }
    }

    // Emby afiseaza textul cu textContent: doar text simplu, fara link apasabil.
    public object Get(GetLoginNotice request)
    {
        var branding = Branding();
        var current = branding.LoginDisclaimer ?? string.Empty;
        var start = current.IndexOf(NoticeMarker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : current.IndexOf(NoticeMarker, start + 1, StringComparison.Ordinal);
        return new ActionResult { Text = end > start ? current[(start + 1)..end].Trim() : null };
    }

    public object Post(SetLoginNotice request)
    {
        var branding = Branding();
        var current = branding.LoginDisclaimer ?? string.Empty;
        var start = current.IndexOf(NoticeMarker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : current.IndexOf(NoticeMarker, start + 1, StringComparison.Ordinal);
        if (end > start)
        {
            current = (current[..start] + current[(end + 1)..]).Trim();
        }

        var text = request.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            current = (current + "\n\n" + NoticeMarker + text + NoticeMarker).Trim();
        }

        branding.LoginDisclaimer = string.IsNullOrEmpty(current) ? null : current;
        _configurationManager.SaveConfiguration("branding", branding);
        return new ActionResult();
    }

    private BrandingOptions Branding() => (BrandingOptions)_configurationManager.GetConfiguration("branding");

    public async Task<object> Get(GetUpdateStatus request) =>
        request.Refresh ? await Updater.CheckAsync(Request.CancellationToken).ConfigureAwait(false) : Updater.Status;

    public async Task<object> Post(InstallUpdate request) => Result(await Updater.InstallAsync(Request.CancellationToken).ConfigureAwait(false));

    public object Post(RollbackUpdate request) => Result(Updater.Rollback());

    public object Post(RestartServer request)
    {
        var playing = _sessionManager.Sessions.Count(s => s.NowPlayingItem != null);
        if (playing > 0 && !request.Force)
        {
            return new ActionResult { Ok = false, Error = "playback_active", ActivePlayback = playing };
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(1000).ConfigureAwait(false);
            _appHost.Restart();
        });
        return new ActionResult();
    }

    public object Post(PurgeData request)
    {
        if (request.Confirm != "STERGE")
        {
            return new ActionResult { Ok = false, Error = "confirm_required" };
        }

        Manager.Store.Clear();
        return new ActionResult();
    }

    private static ActionResult Result(string? error) => new() { Ok = error == null, Error = error };
}
