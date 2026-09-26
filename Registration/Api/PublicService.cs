using System.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using Registration.Flow;
using Registration.Security;
using Registration.Validation;

namespace Registration.Api;

[Route("/Registration/Page", "GET", Summary = "Pagina publica de inregistrare")]
[Unauthenticated]
public sealed class GetRegistrationPage : IReturn<string>
{
    public string? Confirm { get; set; }
}

[Route("/Registration/Assets/{Name}", "GET", Summary = "Stilul si scriptul paginii de inregistrare")]
[Unauthenticated]
public sealed class GetRegistrationAsset : IReturn<string>
{
    public string Name { get; set; } = string.Empty;
}

[Route("/Registration/Status", "GET", Summary = "Daca inregistrarea e deschisa (pentru butonul de pe ecranul de conectare)")]
[Unauthenticated]
public sealed class GetRegistrationStatus : IReturn<RegistrationStatus>
{
}

public sealed class RegistrationStatus
{
    public bool Enabled { get; set; }
}

[Route("/Registration/Info", "GET", Summary = "Starea formularului, campurile si tokenul pentru o cerere noua")]
[Unauthenticated]
public sealed class GetRegistrationInfo : IReturn<RegistrationInfo>
{
    /// <summary>Identificatorul de dispozitiv din localStorage (cand cookie-ul lipseste).</summary>
    public string? Device { get; set; }

    /// <summary>Id-urile conturilor Emby conectate in acest browser, separate prin virgula.</summary>
    public string? Users { get; set; }
}

[Route("/Registration/CheckUsername", "POST", Summary = "Verifica daca un nume de utilizator e disponibil")]
[Unauthenticated]
public sealed class CheckUsername : IReturn<UsernameCheck>
{
    public string? Username { get; set; }
}

[Route("/Registration/Submit", "POST", Summary = "Trimite o cerere de cont")]
[Unauthenticated]
public sealed class SubmitRegistration : SubmitForm, IReturn<SubmitResult>
{
}

[Route("/Registration/Unlock", "POST", Summary = "Deblocheaza formularul cu codul de acces")]
[Unauthenticated]
public sealed class UnlockRegistration : IReturn<UnlockResult>
{
    public string? Code { get; set; }

    public string? Device { get; set; }

    /// <summary>Conturile Emby conectate in browser (ca la Info).</summary>
    public string[]? Users { get; set; }
}

[Route("/Registration/ConfirmEmail", "POST", Summary = "Confirma adresa de e-mail dintr-o cerere")]
[Unauthenticated]
public sealed class ConfirmRegistrationEmail : IReturn<ConfirmResult>
{
    public string? Token { get; set; }
}

public sealed class RegistrationInfo
{
    public bool Open { get; set; }

    /// <summary>closed, not_yet, ended, full, busy.</summary>
    public string? ClosedReason { get; set; }

    public string? ClosedMessage { get; set; }

    /// <summary>Formularul cere intai codul de acces.</summary>
    public bool Locked { get; set; }

    /// <summary>Administrator conectat: verificarile sunt ocolite (test).</summary>
    public bool Admin { get; set; }

    public int CodeLength { get; set; }

    /// <summary>La „blocked”: pana cand.</summary>
    public DateTimeOffset? BlockedUntil { get; set; }

    /// <summary>La „signed_in”: numele contului cu care browserul e deja conectat.</summary>
    public string? ClosedDetail { get; set; }

    /// <summary>Identificatorul de dispozitiv, pentru copia din localStorage.</summary>
    public string? Device { get; set; }

    /// <summary>Id-ul serverului, pentru a gasi conturile Emby din stocarea locala.</summary>
    public string? ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string Mode { get; set; } = RegistrationModes.Approval;

    public string? Token { get; set; }

    public int PowBits { get; set; }

    public string? TurnstileSiteKey { get; set; }

    public int MinFillSeconds { get; set; }

    public bool PinEnabled { get; set; }

    public bool RequireConsent { get; set; }

    public string? PrivacyText { get; set; }

    public bool EmailConfirmation { get; set; }

    public int UsernameMinLength { get; set; }

    public int UsernameMaxLength { get; set; }

    public int PasswordMinLength { get; set; }

    public string DefaultCountry { get; set; } = "RO";

    public List<PhoneCountry> Countries { get; set; } = new();

    /// <summary>Adresa pentru butonul „Intra in Emby” si pentru aplicatii.</summary>
    public string? ServerUrl { get; set; }
}

public sealed class UsernameCheck
{
    public bool Available { get; set; }

    public string? Error { get; set; }
}

public sealed class ConfirmResult
{
    /// <summary>confirmed, already, invalid, expired, rate_limited.</summary>
    public string Outcome { get; set; } = string.Empty;

    public string Mode { get; set; } = RegistrationModes.Approval;
}

/// <summary>Rutele publice (fara autentificare) ale paginii de inregistrare.</summary>
public sealed partial class PublicService : IService, IRequiresRequest
{
    private static readonly Dictionary<string, string> Assets = new(StringComparer.Ordinal)
    {
        ["register.css"] = "text/css; charset=utf-8",
        ["register.js"] = "text/javascript; charset=utf-8",
        ["pow.js"] = "text/javascript; charset=utf-8",
        ["login.js"] = "text/javascript; charset=utf-8",
    };

    private readonly IHttpResultFactory _resultFactory;

    [System.Text.RegularExpressions.GeneratedRegex(@"^[ \t]*//[^\n]*\n", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex CommentLines();

    private readonly IAuthorizationContext _authorization;
    private readonly IUserManager _userManager;

    public PublicService(IHttpResultFactory resultFactory, IAuthorizationContext authorization, IUserManager userManager)
    {
        _resultFactory = resultFactory;
        _authorization = authorization;
        _userManager = userManager;
    }

    public IRequest Request { get; set; } = null!;

    private static RegistrationManager Manager =>
        RegistrationHost.Manager ?? throw new InvalidOperationException("Plugin-ul Inregistrare nu a pornit.");

    /// <summary>
    /// Anteturi de securitate: scripturi doar de pe server si de la Cloudflare Turnstile,
    /// pagina nu poate fi pusa intr-un iframe, nimic nu ramane in cache.
    /// </summary>
    private static Dictionary<string, string> Headers() => new()
    {
        ["Content-Security-Policy"] = "default-src 'self'; script-src 'self' https://challenges.cloudflare.com; "
            + "frame-src https://challenges.cloudflare.com; connect-src 'self'; style-src 'self'; img-src 'self' data:; "
            + "base-uri 'none'; form-action 'self'; frame-ancestors 'none'",
        ["X-Frame-Options"] = "DENY",
        ["X-Content-Type-Options"] = "nosniff",
        ["Referrer-Policy"] = "no-referrer",
        ["Cache-Control"] = "no-store",
        ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()",
    };

    public object Get(GetRegistrationPage request) => Resource("register.html", "text/html; charset=utf-8");

    public object Get(GetRegistrationAsset request) =>
        Assets.TryGetValue(request.Name, out var type) ? Resource(request.Name, type) : throw new FileNotFoundException();

    private object Resource(string name, string contentType)
    {
        using var stream = typeof(PublicService).Assembly.GetManifestResourceStream("Registration.Web." + name)
            ?? throw new FileNotFoundException("Resursa lipseste din plugin: " + name);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        if (name.EndsWith(".js", StringComparison.Ordinal))
        {
            // Comentariile (care descriu verificarile) nu pleaca spre browser.
            text = CommentLines().Replace(text, string.Empty);
        }

        return _resultFactory.GetResult(Request, text.AsSpan(), contentType, Headers());
    }

    /// <summary>
    /// Starea generala, fara verificarile pe vizitator: butonul de pe ecranul de conectare
    /// apare pentru oricine cat timp inregistrarea e deschisa; pagina explica restul.
    /// </summary>
    public object Get(GetRegistrationStatus request) =>
        Json(new RegistrationStatus { Enabled = Manager.ClosedReason(DateTimeOffset.UtcNow) == null });

    public object Get(GetRegistrationInfo request)
    {
        var manager = Manager;
        var settings = RegistrationManager.Settings;
        var now = DateTimeOffset.UtcNow;
        var origin = Origin();
        var headers = Headers();

        // Identificatorul dispozitivului: din cookie, altfel din localStorage, altfel unul nou.
        var device = manager.Devices.Verify(origin.DeviceCookie) != null ? origin.DeviceCookie!
            : manager.Devices.Verify(request.Device) != null ? request.Device! : manager.Devices.Issue();
        var secure = Request.IsSecureConnection || string.Equals(Request.XForwardedProtocol, "https", StringComparison.OrdinalIgnoreCase)
            || settings.PublicUrl.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
        headers["Set-Cookie"] = $"{DeviceIdentity.CookieName}={device}; Path=/; Max-Age=34560000; HttpOnly; SameSite=Strict" + (secure ? "; Secure" : string.Empty);

        string? closed = null;
        string? detail = null;
        var infoKey = "info:" + RateLimiter.IpKey(origin.Ip);
        var admin = origin.IsAdmin;
        if (!admin && !manager.Limits.Allows(infoKey, 60, TimeSpan.FromMinutes(10), now))
        {
            closed = "rate_limited";
        }

        manager.Limits.Record(infoKey, now);
        closed ??= admin ? null : manager.ClosedReason(now);
        var block = closed == null && !admin ? manager.ActiveBlock(RateLimiter.Normalize(origin.Ip)?.ToString(), manager.DeviceHashOf(device), now) : null;
        if (block != null)
        {
            closed = "blocked";
        }

        if (closed == null)
        {
            var network = manager.Network(origin);
            closed = admin ? null : manager.GateVisitor(origin, network);
            var needsCode = !admin && settings.RequireAccessCode && !manager.HasPass(origin.PassCookie, device, RateLimiter.Normalize(origin.Ip)?.ToString(), now);
            if (closed == null && !needsCode && !admin)
            {
                var evidence = new DeviceEvidence
                {
                    Device = device,
                    EmbyUsers = (request.Users ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                };
                var duplicate = manager.FindDuplicates(evidence, origin, network, null, now).FirstOrDefault(x => x.Action == DuplicateActions.Block);
                if (duplicate != null)
                {
                    closed = RegistrationManager.BlockReason(duplicate);
                    detail = duplicate.Code == "signed_in" ? duplicate.Detail[(duplicate.Detail.LastIndexOf(' ') + 1)..] : null;
                }
            }
        }

        var allowed = Validators.Lines(settings.AllowedCountries).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var info = new RegistrationInfo
        {
            Open = closed == null,
            ClosedReason = closed == null ? null : PublicReason(closed),
            ClosedMessage = closed == null || string.IsNullOrWhiteSpace(settings.ClosedMessage) ? null : settings.ClosedMessage.Trim(),
            ServerName = manager.ServerName(manager.DefaultServerName),
            Mode = settings.Mode,
            ServerUrl = string.IsNullOrWhiteSpace(settings.PublicUrl) ? null : settings.PublicUrl.Trim().TrimEnd('/'),

            Device = device,
            ServerId = manager.ServerId,
        };

        if (closed != null)
        {
            return _resultFactory.GetResult(Request, info, headers);
        }

        info.Admin = admin;
        if (!admin && settings.RequireAccessCode && !manager.HasPass(origin.PassCookie, device, RateLimiter.Normalize(origin.Ip)?.ToString(), now))
        {
            info.Locked = true;
            info.CodeLength = RegistrationManager.CodeLength;
            return _resultFactory.GetResult(Request, info, headers);
        }

        var bits = manager.CurrentPowBits(now);
        info.Token = manager.Tokens.Issue(Request.RemoteIp, bits, now);
        info.PowBits = bits;
        info.TurnstileSiteKey = string.IsNullOrWhiteSpace(settings.TurnstileSiteKey) || string.IsNullOrWhiteSpace(settings.TurnstileSecretKey)
            ? null : settings.TurnstileSiteKey.Trim();
        info.MinFillSeconds = settings.MinFillSeconds;
        info.PinEnabled = settings.PinEnabled;
        info.RequireConsent = settings.RequireConsent;
        info.PrivacyText = string.IsNullOrWhiteSpace(settings.PrivacyText) ? null : settings.PrivacyText.Trim();
        info.EmailConfirmation = settings.RequireEmailConfirmation;
        info.UsernameMinLength = settings.UsernameMinLength;
        info.UsernameMaxLength = settings.UsernameMaxLength;
        info.PasswordMinLength = Math.Max(8, settings.PasswordMinLength);
        info.DefaultCountry = settings.DefaultCountry;
        info.Countries = PhoneNumbers.Countries.Where(c => allowed.Count == 0 || allowed.Contains(c.Iso)).ToList();
        return _resultFactory.GetResult(Request, info, headers);
    }

    public object Post(CheckUsername request)
    {
        var error = Manager.CheckUsername(request.Username, Origin(), DateTimeOffset.UtcNow);
        return Json(new UsernameCheck { Available = error == null, Error = error });
    }

    public async Task<object> Post(SubmitRegistration request)
    {
        var result = await Manager.SubmitAsync(request, Origin(), Request.CancellationToken).ConfigureAwait(false);
        return _resultFactory.GetResult(Request, PublicResult(result), Headers());
    }

    public object Post(UnlockRegistration request)
    {
        var manager = Manager;
        var settings = RegistrationManager.Settings;
        var now = DateTimeOffset.UtcNow;
        var origin = Origin();
        var headers = Headers();
        if (settings.RequireSameOrigin && !RegistrationManager.SameOrigin(origin))
        {
            return _resultFactory.GetResult(Request, new UnlockResult { Error = "unavailable" }, headers);
        }

        var network = manager.Network(origin);
        if (manager.GateVisitor(origin, network) != null)
        {
            return _resultFactory.GetResult(Request, new UnlockResult { Error = "unavailable" }, headers);
        }

        var device = origin.DeviceCookie ?? request.Device;
        var result = manager.TryUnlock(request.Code, origin, device, now, request.Users ?? Array.Empty<string>(), network);
        if (result.Ok && result.Pass != null)
        {
            var secure = Request.IsSecureConnection || string.Equals(Request.XForwardedProtocol, "https", StringComparison.OrdinalIgnoreCase)
                || settings.PublicUrl.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
            headers["Set-Cookie"] = $"{RegistrationManager.PassCookieName}={result.Pass}; Path=/; Max-Age={Math.Max(5, settings.CodeUnlockMinutes) * 60}; HttpOnly; SameSite=Strict" + (secure ? "; Secure" : string.Empty);
            result.Pass = null;
        }

        var visible = result.Ok ? new UnlockResult { Ok = true }
            : new UnlockResult { Error = result.Error == "wrong_code" ? "wrong_code" : "unavailable" };
        return _resultFactory.GetResult(Request, visible, headers);
    }

    public object Post(ConfirmRegistrationEmail request) => Json(new ConfirmResult
    {
        Outcome = Manager.ConfirmEmail(request.Token, Origin(), DateTimeOffset.UtcNow),
        Mode = RegistrationManager.Settings.Mode,
    });

    private object Json<T>(T value) where T : class => _resultFactory.GetResult(Request, value, Headers());

    /// <summary>
    /// Vizitatorul afla doar ca nu se poate: motivul exact (cont dublu, retea, blocare,
    /// limite, verificari anti-robot) ramane in jurnal, statistici si in panoul adminului.
    /// </summary>
    internal static string PublicReason(string? reason) => reason switch
    {
        "closed" or "full" or "busy" => "closed",
        "not_yet" or "ended" or "locked" => reason,
        _ => "unavailable",
    };

    internal static SubmitResult PublicResult(SubmitResult result)
    {
        switch (result.Outcome)
        {
            case "Ok":
                return result;
            case "Invalid" when result.Error == "fields":
                foreach (var key in result.Fields.Keys.ToList())
                {
                    result.Fields[key] = result.Fields[key] switch
                    {
                        "email_blocked" => "email_rejected",
                        "phone_taken" or "phone_country" => "phone_rejected",
                        "invite_invalid" => "invite_invalid",
                        var other => other,
                    };
                }

                return result;
            case "Closed" or "Blocked" or "RateLimited":
                return new SubmitResult { Outcome = "Closed", Error = PublicReason(result.Error) };
            default:
                // Token expirat, proof-of-work, Turnstile, prea rapid, automatizare: acelasi mesaj.
                return new SubmitResult { Outcome = "Error", Error = "error" };
        }
    }

    private Origin Origin() => new(Request.RemoteIp, Country(), Request.UserAgent)
    {
        AdminName = AdminName(),
        OriginHeader = RawHeader("Origin"),
        Host = RawHeader("Host"),
        FetchSite = RawHeader("Sec-Fetch-Site")?.ToLowerInvariant(),
        DeviceCookie = DeviceIdentity.FromCookieHeader(RawHeader("Cookie")),
        PassCookie = DeviceIdentity.Cookie(RawHeader("Cookie"), RegistrationManager.PassCookieName),
    };

    /// <summary>
    /// Numele administratorului, daca cererea poarta sesiunea Emby a unui administrator activ
    /// (tokenul din stocarea interfetei web, trimis de pagina). Tokenul e verificat de Emby.
    /// </summary>
    private string? AdminName()
    {
        if (string.IsNullOrEmpty(RawHeader("X-Emby-Token")))
        {
            return null;
        }

        try
        {
            var user = _authorization.GetAuthorizationInfo(Request)?.User;
            if (user == null)
            {
                return null;
            }

            var policy = _userManager.GetUserPolicy(user);
            return policy.IsAdministrator && !policy.IsDisabled ? user.Name : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string? RawHeader(string name)
    {
        var value = Request.Headers.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(value) || value.Length > 4096 ? null : value.Trim();
    }

    private string? Country()
    {
        var value = RawHeader("CF-IPCountry");
        return value == null || value.Length > 8 ? null : value.ToUpperInvariant();
    }
}
