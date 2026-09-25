using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using Registration.Notifications;
using Registration.Security;
using Registration.Storage;
using Registration.Validation;

namespace Registration.Flow;

/// <summary>Ce trimite pagina publica.</summary>
public class SubmitForm
{
    public string? Token { get; set; }

    public string? Pow { get; set; }

    public string? Turnstile { get; set; }

    /// <summary>Capcana: camp ascuns pe care un om nu il vede si nu il completeaza.</summary>
    public string? Website { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? Country { get; set; }

    public string? Phone { get; set; }

    public string? Password { get; set; }

    public string? Pin { get; set; }

    public string? InviteCode { get; set; }

    public bool Consent { get; set; }

    public string? Language { get; set; }
}

/// <summary>Cine trimite: adresa, tara Cloudflare, browserul.</summary>
public sealed record Origin(IPAddress? Ip, string? IpCountry, string? UserAgent);

public sealed class SubmitResult
{
    /// <summary>Ok, Invalid, Closed, RateLimited, Blocked, Expired, Error.</summary>
    public string Outcome { get; set; } = "Ok";

    /// <summary>La Ok: EmailPending, Pending sau Approved (ce ecran de final arata pagina).</summary>
    public string? Status { get; set; }

    /// <summary>Eroare generala (ex. „token_expired”).</summary>
    public string? Error { get; set; }

    /// <summary>Erori pe campuri: camp → cod.</summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    public int RetryAfterSeconds { get; set; }

    public static SubmitResult Fail(string outcome, string error) => new() { Outcome = outcome, Error = error };
}

/// <summary>
/// Fluxul complet al unei cereri de cont. Contul Emby se creeaza inca de la cerere,
/// dezactivat, ascuns si cu drepturi minime: asa parola nu trebuie pastrata nicaieri de
/// plugin (o primeste doar Emby), iar username-ul e rezervat. Aprobarea doar il
/// activeaza; respingerea sau expirarea il sterg.
/// </summary>
public sealed class RegistrationManager
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private readonly IUserManager _userManager;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _submitLock = new(1, 1);

    public RegistrationManager(IUserManager userManager, ILogger logger, RequestStore store, AdminNotifier notifier, WebChecks web)
    {
        _userManager = userManager;
        _logger = logger;
        Store = store;
        Notifier = notifier;
        Web = web;
        Tokens = new FormToken(() => Convert.FromBase64String(Settings.FormSecret));
    }

    public RequestStore Store { get; }

    public AdminNotifier Notifier { get; }

    public WebChecks Web { get; }

    public FormToken Tokens { get; }

    public RateLimiter Limits { get; } = new();

    public PolicyGuard? Guard { get; set; }

    public static PluginConfiguration Settings => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public string ServerName(string fallback) =>
        string.IsNullOrWhiteSpace(Settings.ServerDisplayName) ? fallback : Settings.ServerDisplayName.Trim();

    public string DefaultServerName { get; set; } = "Emby";

    // --- Stare ------------------------------------------------------------------------------

    /// <summary>Null daca formularul primeste cereri, altfel motivul (closed, not_yet, ended, full, busy).</summary>
    public string? ClosedReason(DateTimeOffset now)
    {
        var settings = Settings;
        if (!settings.RegistrationOpen)
        {
            return "closed";
        }

        if (TryDate(settings.OpenFrom, out var from) && now < from)
        {
            return "not_yet";
        }

        if (TryDate(settings.OpenUntil, out var until) && now >= until)
        {
            return "ended";
        }

        var (accounts, waiting, today) = Store.Read(d => (
            d.Requests.Count(r => r.Status is RequestStatus.Approved || RequestStatus.IsWaiting(r.Status)),
            d.Requests.Count(r => RequestStatus.IsWaiting(r.Status)),
            d.Requests.Count(r => now - r.CreatedAt < Day)));

        if (settings.MaxAccounts > 0 && accounts >= settings.MaxAccounts)
        {
            return "full";
        }

        if ((settings.MaxPending > 0 && waiting >= settings.MaxPending) || (settings.MaxRequestsPerDay > 0 && today >= settings.MaxRequestsPerDay))
        {
            return "busy";
        }

        return null;
    }

    private static bool TryDate(string value, out DateTimeOffset date) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date) && !string.IsNullOrWhiteSpace(value);

    public int PowBits => Settings.ProofOfWorkEnabled ? Math.Clamp(Settings.ProofOfWorkBits, 8, 26) : 0;

    // --- Verificarea username-ului ---------------------------------------------------------------

    public string? CheckUsername(string? username, Origin origin, DateTimeOffset now)
    {
        var key = "check:" + RateLimiter.IpKey(origin.Ip);
        if (!Limits.Allows(key, 60, TimeSpan.FromMinutes(10), now))
        {
            return "rate_limited";
        }

        Limits.Record(key, now);
        var settings = Settings;
        var normalized = Validators.NormalizeUsername(username);
        return Validators.Username(normalized, settings.UsernameMinLength, settings.UsernameMaxLength, Validators.Lines(settings.ReservedUsernames))
            ?? (UsernameTaken(normalized) ? "username_taken" : null);
    }

    private bool UsernameTaken(string normalized) =>
        _userManager.GetUserByName(normalized) != null
        || Store.Read(d => d.Requests.Any(r => RequestStatus.IsWaiting(r.Status) && r.Username == normalized));

    // --- Trimiterea formularului -----------------------------------------------------------------

    public async Task<SubmitResult> SubmitAsync(SubmitForm form, Origin origin, CancellationToken cancellationToken)
    {
        var settings = Settings;
        var now = DateTimeOffset.UtcNow;

        var closed = ClosedReason(now);
        if (closed != null)
        {
            return SubmitResult.Fail("Closed", closed);
        }

        if (new IpList(Validators.Lines(settings.BlockedIps)).Contains(origin.Ip))
        {
            Store.Count("blocked_ip", now);
            return SubmitResult.Fail("Blocked", "blocked");
        }

        // Limitele se aplica fiecarei trimiteri, reusite sau nu: un robot care greseste tot consuma din ele.
        var ipKey = RateLimiter.IpKey(origin.Ip);
        var netKey = RateLimiter.SubnetKey(origin.Ip);
        var limited = !Limits.Allows(ipKey, settings.MaxPerIpPerHour, Hour, now) ? (ipKey, Hour)
            : !Limits.Allows(netKey, settings.MaxPerSubnetPerDay, Day, now) ? (netKey, Day)
            : !Limits.Allows("global", settings.MaxGlobalPerHour, Hour, now) ? ("global", Hour)
            : ((string, TimeSpan)?)null;
        if (limited != null)
        {
            Store.Count("rate_limited", now);
            if (Limits.Count("abuse-notified:" + netKey, Day, now) == 0)
            {
                Limits.Record("abuse-notified:" + netKey, now);
                Notifier.Notify(NotifyEvents.Abuse, "Înregistrare: prea multe încercări",
                    $"Limita de cereri a fost atinsă ({limited.Value.Item1}), ultima de la {origin.Ip}.", warning: true);
            }

            return new SubmitResult
            {
                Outcome = "RateLimited",
                Error = "rate_limited",
                RetryAfterSeconds = (int)Math.Ceiling(Limits.RetryAfter(limited.Value.Item1, limited.Value.Item2, now).TotalSeconds),
            };
        }

        Limits.Record(ipKey, now);
        Limits.Record(netKey, now);
        Limits.Record("global", now);

        // Capcana: raspundem ca si cum cererea ar fi reusit, ca robotul sa nu invete nimic.
        if (!string.IsNullOrEmpty(form.Website))
        {
            Store.Count("bot_honeypot", now);
            return new SubmitResult { Status = RequestStatus.Pending };
        }

        var token = Tokens.Verify(form.Token, origin.Ip, now);
        if (token.Error != null)
        {
            Store.Count("bot_token", now);
            return SubmitResult.Fail("Expired", token.Error);
        }

        if (now - token.Issued < TimeSpan.FromSeconds(Math.Max(0, settings.MinFillSeconds)))
        {
            Store.Count("bot_too_fast", now);
            return SubmitResult.Fail("Invalid", "too_fast");
        }

        if (!ProofOfWork.Verify(form.Token!, form.Pow, token.PowBits))
        {
            Store.Count("bot_pow", now);
            return SubmitResult.Fail("Invalid", "bot_check");
        }

        if (!string.IsNullOrWhiteSpace(settings.TurnstileSecretKey)
            && !await Web.VerifyTurnstileAsync(settings.TurnstileSecretKey.Trim(), form.Turnstile, origin.Ip, cancellationToken).ConfigureAwait(false))
        {
            Store.Count("bot_turnstile", now);
            return SubmitResult.Fail("Invalid", "bot_check");
        }

        var input = Normalize(form);
        var fields = Validate(input, form, settings, now);
        if (fields.Count == 0 && settings.CheckPwnedPasswords)
        {
            var pwned = await Web.PwnedCountAsync(form.Password!, cancellationToken).ConfigureAwait(false);
            if (pwned > 0)
            {
                fields["password"] = "password_pwned";
            }
        }

        if (fields.Count > 0)
        {
            Store.Count("invalid", now);
            return new SubmitResult { Outcome = "Invalid", Error = "fields", Fields = fields };
        }

        // Tokenul se consuma abia acum: dupa o greseala de completare, formularul se poate retrimite.
        if (!Tokens.Consume(form.Token!, now))
        {
            Store.Count("bot_token_reuse", now);
            return SubmitResult.Fail("Expired", "token_expired");
        }

        await _submitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await CreateAsync(input, form, origin, settings, now).ConfigureAwait(false);
        }
        finally
        {
            _submitLock.Release();
        }
    }

    private sealed record Input(string Username, string FirstName, string LastName, string Email, string? Phone, string? Pin, string? Invite, string Language);

    private static Input Normalize(SubmitForm form) => new(
        Validators.NormalizeUsername(form.Username),
        Validators.NormalizeName(form.FirstName),
        Validators.NormalizeName(form.LastName),
        Validators.NormalizeEmail(form.Email),
        PhoneNumbers.Normalize(form.Phone, form.Country),
        string.IsNullOrWhiteSpace(form.Pin) ? null : form.Pin.Trim(),
        string.IsNullOrWhiteSpace(form.InviteCode) ? null : form.InviteCode.Trim().ToUpperInvariant(),
        string.Equals(form.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ro");

    private Dictionary<string, string> Validate(Input input, SubmitForm form, PluginConfiguration settings, DateTimeOffset now)
    {
        var fields = new Dictionary<string, string>();

        void Check(string field, string? error)
        {
            if (error != null)
            {
                fields.TryAdd(field, error);
            }
        }

        Check("username", Validators.Username(input.Username, settings.UsernameMinLength, settings.UsernameMaxLength, Validators.Lines(settings.ReservedUsernames)));
        if (!fields.ContainsKey("username") && UsernameTaken(input.Username))
        {
            fields["username"] = "username_taken";
        }

        Check("firstName", Validators.Name(input.FirstName));
        Check("lastName", Validators.Name(input.LastName));
        Check("email", Validators.Email(input.Email));
        if (!fields.ContainsKey("email"))
        {
            var domain = Validators.EmailDomain(input.Email);
            var blocked = Validators.Lines(settings.BlockedEmailDomains).Any(d =>
                domain.Equals(d, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
            if (blocked || (settings.BlockDisposableEmail && DisposableEmail.IsDisposable(domain)))
            {
                fields["email"] = "email_blocked";
            }
        }

        var allowed = Validators.Lines(settings.AllowedCountries).ToList();
        Check("phone", input.Phone == null ? "phone_invalid" : PhoneNumbers.Validate(input.Phone, allowed));
        Check("password", Validators.Password(form.Password, Math.Max(8, settings.PasswordMinLength), input.Username));

        if (settings.PinEnabled && input.Pin != null)
        {
            Check("pin", Validators.Pin(input.Pin));
        }

        if (settings.RequireConsent && !form.Consent)
        {
            fields["consent"] = "consent_required";
        }

        if (settings.Mode == RegistrationModes.InviteOnly)
        {
            var valid = input.Invite != null && Store.Read(d => d.Invites.Any(i => i.Code == input.Invite && i.IsUsable(now)));
            if (!valid)
            {
                fields["inviteCode"] = "invite_invalid";
            }
        }

        return fields;
    }

    private async Task<SubmitResult> CreateAsync(Input input, SubmitForm form, Origin origin, PluginConfiguration settings, DateTimeOffset now)
    {
        // Verificare repetata sub lacat: doua cereri simultane cu acelasi nume.
        if (UsernameTaken(input.Username))
        {
            return new SubmitResult { Outcome = "Invalid", Error = "fields", Fields = { ["username"] = "username_taken" } };
        }

        // Aceeasi adresa de e-mail inca activa: raspuns identic cu succesul (nu dezvaluim ce
        // adrese sunt inregistrate), dar nu se creeaza nimic.
        var duplicate = Store.Read(d => d.Requests.Any(r =>
            r.Email == input.Email && (r.Status == RequestStatus.Approved || RequestStatus.IsWaiting(r.Status))));
        if (duplicate)
        {
            Store.Count("duplicate_email", now);
            _logger.Info("Inregistrare: cerere noua cu o adresa de e-mail deja folosita, ignorata ({0})", origin.Ip);
            return new SubmitResult { Status = settings.RequireEmailConfirmation && Mailer.IsConfigured(settings) ? RequestStatus.EmailPending : RequestStatus.Pending };
        }

        var confirmEmail = settings.RequireEmailConfirmation && Mailer.IsConfigured(settings) && !string.IsNullOrWhiteSpace(settings.PublicUrl);
        var automatic = settings.Mode == RegistrationModes.Automatic;
        var status = confirmEmail ? RequestStatus.EmailPending : automatic ? RequestStatus.Approved : RequestStatus.Pending;

        User user;
        try
        {
            user = await _userManager.CreateUser(input.Username, AccessPolicy.Build(settings, disabled: status != RequestStatus.Approved)).ConfigureAwait(false);
            await _userManager.ChangePassword(user, form.Password!).ConfigureAwait(false);
            if (settings.PinEnabled && input.Pin != null)
            {
                var configuration = _userManager.GetUserConfiguration(user);
                configuration.ProfilePin = input.Pin;
                _userManager.UpdateConfiguration(user, configuration);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Inregistrare: contul {0} nu a putut fi creat", ex, input.Username);
            var orphan = _userManager.GetUserByName(input.Username);
            if (orphan != null)
            {
                await DeleteUserQuietly(orphan).ConfigureAwait(false);
            }

            return SubmitResult.Fail("Error", "server_error");
        }

        string? emailToken = null;
        var record = new RegistrationRecord
        {
            Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant(),
            Status = status,
            UserId = user.Id.ToString("N"),
            Username = input.Username,
            FirstName = input.FirstName,
            LastName = input.LastName,
            Email = input.Email,
            Phone = input.Phone!,
            HasPin = settings.PinEnabled && input.Pin != null,
            InviteCode = settings.Mode == RegistrationModes.InviteOnly ? input.Invite : null,
            Ip = RateLimiter.Normalize(origin.Ip)?.ToString() ?? string.Empty,
            IpCountry = origin.IpCountry,
            UserAgent = origin.UserAgent is { Length: > 300 } ua ? ua[..300] : origin.UserAgent,
            Language = input.Language,
            CreatedAt = now,
            ConsentAt = form.Consent ? now : null,
        };

        if (confirmEmail)
        {
            emailToken = FormToken.Base64Url(RandomNumberGenerator.GetBytes(24));
            record.EmailTokenHash = Hash(emailToken);
            record.EmailTokenExpires = now.AddHours(Math.Max(1, settings.EmailConfirmationHours));
        }

        if (status == RequestStatus.Approved)
        {
            record.DecidedAt = now;
            record.DecidedBy = "automat";
            record.AccountExpiresAt = settings.AccountExpiryDays > 0 ? now.AddDays(settings.AccountExpiryDays) : null;
        }

        Store.Write(d =>
        {
            d.Requests.Add(record);
            if (record.InviteCode != null)
            {
                var invite = d.Invites.FirstOrDefault(i => i.Code == record.InviteCode);
                if (invite != null)
                {
                    invite.Uses++;
                }
            }

            RequestStore.Increment(d, "requests", now);
        });

        _logger.Info("Inregistrare: cerere noua {0} ({1}) de la {2}, stare {3}", record.Username, record.Id, record.Ip, record.Status);

        var server = ServerName(DefaultServerName);
        if (emailToken != null)
        {
            var link = PublicUrl() + "/emby/Registration/Page?confirm=" + emailToken;
            SendToUser(record, Texts.ConfirmEmail(record.Language, server, record.FirstName, link, settings.EmailConfirmationHours));
        }
        else if (status == RequestStatus.Pending)
        {
            SendToUser(record, Texts.Received(record.Language, server, record.FirstName, record.Username));
        }

        if (status == RequestStatus.Approved)
        {
            Guard?.ReapplyLater(user.InternalId);
            SendToUser(record, Texts.Approved(record.Language, server, record.FirstName, record.Username, PublicUrlOrNull()));
        }

        Notifier.Notify(NotifyEvents.NewRequest,
            status == RequestStatus.Approved ? $"Înregistrare: cont nou {record.Username}" : $"Înregistrare: cerere nouă de la {record.Username}",
            Describe(record) + (status == RequestStatus.EmailPending ? " Așteaptă confirmarea adresei de e-mail." : status == RequestStatus.Pending ? " Așteaptă aprobarea." : " Aprobat automat."));

        return new SubmitResult { Status = status };
    }

    private static string Describe(RegistrationRecord r) =>
        $"{r.FirstName} {r.LastName} ({r.Username}), {r.Email}, {r.Phone}, de la {r.Ip}{(r.IpCountry == null ? string.Empty : " / " + r.IpCountry)}.";

    // --- Confirmarea e-mailului --------------------------------------------------------------------

    /// <summary>Rezultat: confirmed, already, invalid, expired.</summary>
    public string ConfirmEmail(string? token, Origin origin, DateTimeOffset now)
    {
        var key = "confirm:" + RateLimiter.IpKey(origin.Ip);
        if (!Limits.Allows(key, 20, Hour, now))
        {
            return "rate_limited";
        }

        Limits.Record(key, now);
        if (string.IsNullOrEmpty(token) || token.Length > 64)
        {
            return "invalid";
        }

        var hash = Hash(token);
        var (outcome, record) = Store.Write(d =>
        {
            var r = d.Requests.FirstOrDefault(x => x.EmailTokenHash == hash);
            if (r == null)
            {
                return ("invalid", (RegistrationRecord?)null);
            }

            if (r.Status != RequestStatus.EmailPending)
            {
                return ("already", r);
            }

            if (r.EmailTokenExpires < now)
            {
                return ("expired", r);
            }

            r.EmailConfirmedAt = now;
            r.Status = RequestStatus.Pending;
            return ("confirmed", r);
        });

        if (outcome == "confirmed" && record != null)
        {
            if (Settings.Mode == RegistrationModes.Automatic)
            {
                Approve(record.Id, "automat", now);
            }
            else
            {
                Notifier.Notify(NotifyEvents.EmailConfirmed, $"Înregistrare: {record.Username} și-a confirmat adresa", Describe(record) + " Așteaptă aprobarea.");
            }
        }

        return outcome;
    }

    // --- Decizii admin ---------------------------------------------------------------------------

    public RegistrationRecord? Find(string id) => Store.Read(d => d.Requests.FirstOrDefault(r => r.Id == id));

    public User? UserOf(RegistrationRecord record) =>
        Guid.TryParse(record.UserId, out var id) ? _userManager.GetUserById(id) : null;

    /// <summary>Activeaza contul. Intoarce null la succes, altfel motivul.</summary>
    public string? Approve(string id, string admin, DateTimeOffset now)
    {
        var settings = Settings;
        var record = Find(id);
        if (record == null)
        {
            return "not_found";
        }

        if (record.Status is not (RequestStatus.Pending or RequestStatus.EmailPending))
        {
            return "not_waiting";
        }

        var user = UserOf(record);
        if (user == null)
        {
            Store.Write(d => MarkDeleted(d, record.Id, now));
            return "user_missing";
        }

        Store.Write(d =>
        {
            var r = d.Requests.First(x => x.Id == id);
            r.Status = RequestStatus.Approved;
            r.DecidedAt = now;
            r.DecidedBy = admin;
            r.AccountExpiresAt = settings.AccountExpiryDays > 0 ? now.AddDays(settings.AccountExpiryDays) : null;
            RequestStore.Increment(d, "approved", now);
        });

        // Politica completa din nou: intre timp se pot fi schimbat bibliotecile alese.
        var policy = _userManager.GetUserPolicy(user);
        AccessPolicy.ApplyConfigured(policy, settings);
        AccessPolicy.ApplyForbidden(policy);
        policy.IsDisabled = false;
        Guard?.Update(user, policy);

        record = Find(id)!;
        SendToUser(record, Texts.Approved(record.Language, ServerName(DefaultServerName), record.FirstName, record.Username, PublicUrlOrNull()));
        Notifier.Notify(NotifyEvents.Decision, $"Înregistrare: {record.Username} aprobat", $"Aprobat de {admin}. " + Describe(record));
        _logger.Info("Inregistrare: {0} aprobat de {1}", record.Username, admin);
        return null;
    }

    public async Task<string?> RejectAsync(string id, string admin, string? reason, bool notifyUser, DateTimeOffset now)
    {
        var record = Find(id);
        if (record == null)
        {
            return "not_found";
        }

        if (!RequestStatus.IsWaiting(record.Status))
        {
            return "not_waiting";
        }

        var user = UserOf(record);
        if (user != null)
        {
            await DeleteUserQuietly(user).ConfigureAwait(false);
        }

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        Store.Write(d =>
        {
            var r = d.Requests.First(x => x.Id == id);
            r.Status = RequestStatus.Rejected;
            r.DecidedAt = now;
            r.DecidedBy = admin;
            r.Reason = reason;
            r.EmailTokenHash = null;
            RequestStore.Increment(d, "rejected", now);
        });

        if (notifyUser)
        {
            SendToUser(record, Texts.Rejected(record.Language, ServerName(DefaultServerName), record.FirstName, reason));
        }

        Notifier.Notify(NotifyEvents.Decision, $"Înregistrare: {record.Username} respins", $"Respins de {admin}." + (reason == null ? string.Empty : " Motiv: " + reason));
        _logger.Info("Inregistrare: {0} respins de {1}", record.Username, admin);
        return null;
    }

    /// <summary>Sterge cererea si datele ei; optional si contul Emby.</summary>
    public async Task<string?> DeleteAsync(string id, bool deleteUser)
    {
        var record = Find(id);
        if (record == null)
        {
            return "not_found";
        }

        if (deleteUser || RequestStatus.IsWaiting(record.Status))
        {
            var user = UserOf(record);
            if (user != null)
            {
                await DeleteUserQuietly(user).ConfigureAwait(false);
            }
        }

        Store.Write(d => d.Requests.RemoveAll(r => r.Id == id));
        return null;
    }

    /// <summary>Contul iese de sub paznicul de politica (adminul ii poate da drepturi in plus) sau revine.</summary>
    public string? SetManaged(string id, bool managed)
    {
        var found = Store.Write(d =>
        {
            var r = d.Requests.FirstOrDefault(x => x.Id == id);
            if (r != null)
            {
                r.Managed = managed;
            }

            return r != null;
        });

        if (found && managed && Find(id) is { } record && UserOf(record) is { } user)
        {
            Guard?.Enforce(user);
        }

        return found ? null : "not_found";
    }

    public string? ResendConfirmation(string id, DateTimeOffset now)
    {
        var settings = Settings;
        var record = Find(id);
        if (record == null)
        {
            return "not_found";
        }

        if (record.Status != RequestStatus.EmailPending)
        {
            return "not_waiting";
        }

        if (!Mailer.IsConfigured(settings) || string.IsNullOrWhiteSpace(settings.PublicUrl))
        {
            return "smtp_missing";
        }

        var token = FormToken.Base64Url(RandomNumberGenerator.GetBytes(24));
        Store.Write(d =>
        {
            var r = d.Requests.First(x => x.Id == id);
            r.EmailTokenHash = Hash(token);
            r.EmailTokenExpires = now.AddHours(Math.Max(1, settings.EmailConfirmationHours));
        });

        SendToUser(record, Texts.ConfirmEmail(record.Language, ServerName(DefaultServerName), record.FirstName,
            PublicUrl() + "/emby/Registration/Page?confirm=" + token, settings.EmailConfirmationHours));
        return null;
    }

    // --- Intretinere periodica -----------------------------------------------------------------------

    /// <summary>
    /// Cererile neaprobate la timp expira (contul dezactivat se sterge), conturile de proba
    /// se dezactiveaza, iar cererile vechi respinse sau expirate se sterg definitiv.
    /// </summary>
    public async Task MaintainAsync(DateTimeOffset now)
    {
        var settings = Settings;
        var expiry = TimeSpan.FromDays(Math.Max(1, settings.PendingExpiryDays));

        var expired = Store.Read(d => d.Requests
            .Where(r => RequestStatus.IsWaiting(r.Status)
                && (now - r.CreatedAt > expiry || (r.Status == RequestStatus.EmailPending && r.EmailTokenExpires < now - Day)))
            .ToList());

        foreach (var record in expired)
        {
            if (UserOf(record) is { } user)
            {
                await DeleteUserQuietly(user).ConfigureAwait(false);
            }

            Store.Write(d =>
            {
                var r = d.Requests.First(x => x.Id == record.Id);
                r.Status = RequestStatus.Expired;
                r.DecidedAt = now;
                r.DecidedBy = "expirare";
                r.EmailTokenHash = null;
            });
            _logger.Info("Inregistrare: cererea {0} ({1}) a expirat", record.Username, record.Id);
        }

        var trials = Store.Read(d => d.Requests.Where(r => r.Status == RequestStatus.Approved && r.AccountExpiresAt < now).ToList());
        foreach (var record in trials)
        {
            if (UserOf(record) is { } user)
            {
                var policy = _userManager.GetUserPolicy(user);
                if (!policy.IsDisabled)
                {
                    policy.IsDisabled = true;
                    Guard?.Update(user, policy);
                    Notifier.Notify(NotifyEvents.Decision, $"Înregistrare: contul {record.Username} a expirat", "Contul de probă a fost dezactivat.");
                }
            }

            Store.Write(d => d.Requests.First(x => x.Id == record.Id).AccountExpiresAt = null);
        }

        var retention = TimeSpan.FromDays(Math.Max(1, settings.RetentionDays));
        Store.Write(d => d.Requests.RemoveAll(r =>
            r.Status is RequestStatus.Rejected or RequestStatus.Expired or RequestStatus.Deleted
            && now - (r.DecidedAt ?? r.CreatedAt) > retention));

        Limits.Cleanup(Day, now);
    }

    /// <summary>Contul Emby a fost sters: pastram doar urma cererii, fara datele personale.</summary>
    public void OnUserDeleted(Guid userId, DateTimeOffset now)
    {
        var id = userId.ToString("N");
        Store.Write(d =>
        {
            foreach (var r in d.Requests.Where(r => r.UserId == id && r.Status != RequestStatus.Deleted))
            {
                MarkDeleted(d, r.Id, now);
            }
        });
    }

    private static void MarkDeleted(StoreData data, string id, DateTimeOffset now)
    {
        var r = data.Requests.First(x => x.Id == id);
        if (RequestStatus.IsWaiting(r.Status) || r.Status == RequestStatus.Approved)
        {
            r.Status = RequestStatus.Deleted;
            r.DecidedAt = now;
        }

        r.FirstName = string.Empty;
        r.LastName = string.Empty;
        r.Email = string.Empty;
        r.Phone = string.Empty;
        r.Ip = string.Empty;
        r.UserAgent = null;
        r.EmailTokenHash = null;
    }

    // --- Utilitare ---------------------------------------------------------------------------------

    private async Task DeleteUserQuietly(User user)
    {
        try
        {
            await _userManager.DeleteUser(user).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Inregistrare: contul {0} nu a putut fi sters", ex, user.Name);
        }
    }

    private void SendToUser(RegistrationRecord record, Texts.Mail mail)
    {
        var settings = Settings;
        if (!settings.SendUserEmails && record.Status != RequestStatus.EmailPending)
        {
            return;
        }

        if (!Mailer.IsConfigured(settings) || string.IsNullOrEmpty(record.Email))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Mailer.SendAsync(settings, new[] { record.Email }, mail.Subject, mail.Body, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Inregistrare: e-mailul catre {0} nu a putut fi trimis", ex, record.Username);
            }
        });
    }

    private static string PublicUrl() => Settings.PublicUrl.Trim().TrimEnd('/');

    private static string? PublicUrlOrNull() => string.IsNullOrWhiteSpace(Settings.PublicUrl) ? null : PublicUrl();

    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
