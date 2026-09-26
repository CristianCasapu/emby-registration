using System.Net;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using Registration.Flow;
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

[Route("/Registration/Info", "GET", Summary = "Starea formularului, campurile si tokenul pentru o cerere noua")]
[Unauthenticated]
public sealed class GetRegistrationInfo : IReturn<RegistrationInfo>
{
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
public sealed class PublicService : IService, IRequiresRequest
{
    private static readonly Dictionary<string, string> Assets = new(StringComparer.Ordinal)
    {
        ["register.css"] = "text/css; charset=utf-8",
        ["register.js"] = "text/javascript; charset=utf-8",
        ["pow.js"] = "text/javascript; charset=utf-8",
        ["login.js"] = "text/javascript; charset=utf-8",
    };

    private readonly IHttpResultFactory _resultFactory;

    public PublicService(IHttpResultFactory resultFactory)
    {
        _resultFactory = resultFactory;
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
        return _resultFactory.GetResult(Request, reader.ReadToEnd().AsSpan(), contentType, Headers());
    }

    public object Get(GetRegistrationInfo request)
    {
        var manager = Manager;
        var settings = RegistrationManager.Settings;
        var now = DateTimeOffset.UtcNow;
        var closed = manager.ClosedReason(now);
        var allowed = Validators.Lines(settings.AllowedCountries).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var info = new RegistrationInfo
        {
            Open = closed == null,
            ClosedReason = closed,
            ClosedMessage = closed == null || string.IsNullOrWhiteSpace(settings.ClosedMessage) ? null : settings.ClosedMessage.Trim(),
            ServerName = manager.ServerName(manager.DefaultServerName),
            Mode = settings.Mode,
            ServerUrl = string.IsNullOrWhiteSpace(settings.PublicUrl) ? null : settings.PublicUrl.Trim().TrimEnd('/'),
        };

        if (closed != null)
        {
            return Json(info);
        }

        info.Token = manager.Tokens.Issue(Request.RemoteIp, manager.PowBits, now);
        info.PowBits = manager.PowBits;
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
        return Json(info);
    }

    public object Post(CheckUsername request)
    {
        var error = Manager.CheckUsername(request.Username, Origin(), DateTimeOffset.UtcNow);
        return Json(new UsernameCheck { Available = error == null, Error = error });
    }

    public async Task<object> Post(SubmitRegistration request)
    {
        var result = await Manager.SubmitAsync(request, Origin(), Request.CancellationToken).ConfigureAwait(false);
        var headers = Headers();
        if (result.Outcome == "RateLimited")
        {
            headers["Retry-After"] = Math.Max(1, result.RetryAfterSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return _resultFactory.GetResult(Request, result, headers);
    }

    public object Post(ConfirmRegistrationEmail request) => Json(new ConfirmResult
    {
        Outcome = Manager.ConfirmEmail(request.Token, Origin(), DateTimeOffset.UtcNow),
        Mode = RegistrationManager.Settings.Mode,
    });

    private object Json<T>(T value) where T : class => _resultFactory.GetResult(Request, value, Headers());

    private Origin Origin() => new(Request.RemoteIp, Header("CF-IPCountry"), Request.UserAgent);

    private string? Header(string name)
    {
        var value = Request.Headers.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(value) || value.Length > 8 ? null : value.Trim().ToUpperInvariant();
    }
}
