using MediaBrowser.Model.Plugins;

namespace Registration;

/// <summary>
/// Setarile plugin-ului. Valorile implicite de aici trebuie sa fie aceleasi cu cele din
/// schema paginii de configurare (Configuration/registration.js).
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    // --- General ---------------------------------------------------------------------------

    /// <summary>Pagina publica primeste cereri. Oprit la instalare: nimic nu se deschide singur.</summary>
    public bool RegistrationOpen { get; set; }

    /// <summary>Optional, ISO 8601: inregistrarea e deschisa doar incepand de atunci.</summary>
    public string OpenFrom { get; set; } = string.Empty;

    /// <summary>Optional, ISO 8601: inregistrarea se inchide singura atunci.</summary>
    public string OpenUntil { get; set; } = string.Empty;

    /// <summary>Approval (aprobare manuala), Automatic sau InviteOnly (cod de invitatie + aprobare).</summary>
    public string Mode { get; set; } = RegistrationModes.Approval;

    /// <summary>Conturi create prin plugin, in total; 0 = fara limita.</summary>
    public int MaxAccounts { get; set; }

    /// <summary>Cereri acceptate pe zi (toate adresele); 0 = fara limita.</summary>
    public int MaxRequestsPerDay { get; set; } = 20;

    /// <summary>Cereri in asteptare in acelasi timp; peste limita formularul se inchide temporar.</summary>
    public int MaxPending { get; set; } = 50;

    /// <summary>Cererile neaprobate (si conturile lor dezactivate) se sterg dupa atatea zile.</summary>
    public int PendingExpiryDays { get; set; } = 7;

    /// <summary>Cererile respinse sau expirate se sterg din jurnal dupa atatea zile.</summary>
    public int RetentionDays { get; set; } = 30;

    public string ClosedMessage { get; set; } = string.Empty;

    /// <summary>Adresa publica a serverului (ex. https://exemplu.ro:2096), pentru linkurile din e-mailuri.</summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Numele afisat pe pagina de inregistrare; gol = numele serverului Emby.</summary>
    public string ServerDisplayName { get; set; } = string.Empty;

    // --- Formular --------------------------------------------------------------------------

    public bool PinEnabled { get; set; } = true;

    public string DefaultCountry { get; set; } = "RO";

    /// <summary>Coduri ISO de tara separate prin virgula; gol = toate.</summary>
    public string AllowedCountries { get; set; } = string.Empty;

    public int UsernameMinLength { get; set; } = 3;

    public int UsernameMaxLength { get; set; } = 32;

    /// <summary>Nume interzise, unul pe linie, pe langa lista inclusa (admin, root, emby...).</summary>
    public string ReservedUsernames { get; set; } = string.Empty;

    public int PasswordMinLength { get; set; } = 10;

    public bool CheckPwnedPasswords { get; set; } = true;

    public bool RequireConsent { get; set; } = true;

    /// <summary>Textul de confidentialitate afisat langa bifa de acord (text simplu).</summary>
    public string PrivacyText { get; set; } = string.Empty;

    // --- Acces implicit ---------------------------------------------------------------------

    /// <summary>true = toate bibliotecile (si cele adaugate ulterior); false = doar <see cref="Libraries"/>.</summary>
    public bool AllLibraries { get; set; }

    /// <summary>Guid-urile bibliotecilor (format N), cand <see cref="AllLibraries"/> e oprit.</summary>
    public string[] Libraries { get; set; } = Array.Empty<string>();

    public bool EnableLiveTv { get; set; }

    /// <summary>Redari simultane; 0 = fara limita.</summary>
    public int StreamLimit { get; set; } = 1;

    /// <summary>Bitrate maxim in afara retelei, in Mbps; 0 = fara limita.</summary>
    public int RemoteBitrateLimitMbps { get; set; }

    /// <summary>Cont dezactivat automat dupa atatea zile de la aprobare; 0 = niciodata.</summary>
    public int AccountExpiryDays { get; set; }

    /// <summary>Paznicul de politica readuce drepturile interzise la „nu” pentru conturile gestionate.</summary>
    public bool EnforcePolicy { get; set; } = true;

    // --- Anti-roboti -------------------------------------------------------------------------

    public string TurnstileSiteKey { get; set; } = string.Empty;

    public string TurnstileSecretKey { get; set; } = string.Empty;

    public bool ProofOfWorkEnabled { get; set; } = true;

    /// <summary>Biti de zero ceruti in SHA-256; fiecare bit dubleaza munca clientului (18 ≈ 1–3 s pe telefon).</summary>
    public int ProofOfWorkBits { get; set; } = 18;

    public int MinFillSeconds { get; set; } = 4;

    public int MaxPerIpPerHour { get; set; } = 5;

    public int MaxPerSubnetPerDay { get; set; } = 20;

    public int MaxGlobalPerHour { get; set; } = 30;

    public bool BlockDisposableEmail { get; set; } = true;

    /// <summary>Domenii de e-mail interzise, unul pe linie, pe langa lista inclusa.</summary>
    public string BlockedEmailDomains { get; set; } = string.Empty;

    /// <summary>Adrese sau retele (CIDR) interzise, una pe linie.</summary>
    public string BlockedIps { get; set; } = string.Empty;

    // --- Notificari si e-mail ------------------------------------------------------------------

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool SmtpStartTls { get; set; } = true;

    public string SmtpUser { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    public string SmtpFrom { get; set; } = string.Empty;

    /// <summary>Cererea ajunge la aprobare doar dupa ce utilizatorul isi confirma adresa de e-mail.</summary>
    public bool RequireEmailConfirmation { get; set; }

    public int EmailConfirmationHours { get; set; } = 24;

    /// <summary>E-mailuri catre cel care se inregistreaza: cerere primita, aprobata, respinsa.</summary>
    public bool SendUserEmails { get; set; } = true;

    public bool NotifyActivityLog { get; set; } = true;

    public bool NotifyEmail { get; set; }

    /// <summary>Adresele adminului, separate prin virgula.</summary>
    public string NotifyEmailTo { get; set; } = string.Empty;

    public bool NotifyTelegram { get; set; }

    public string TelegramBotToken { get; set; } = string.Empty;

    public string TelegramChatId { get; set; } = string.Empty;

    public bool NotifyOnNewRequest { get; set; } = true;

    public bool NotifyOnEmailConfirmed { get; set; }

    public bool NotifyOnDecision { get; set; }

    public bool NotifyOnAbuse { get; set; } = true;

    /// <summary>Notificarile din e-mail si Telegram se strang intr-un singur mesaj la atatea minute; 0 = imediat.</summary>
    public int NotifyBatchMinutes { get; set; }

    // --- Actualizari -------------------------------------------------------------------------

    public bool AutoCheckUpdates { get; set; } = true;

    public bool IncludePrereleases { get; set; }

    public string GitHubRepository { get; set; } = "CristianCasapu/emby-registration";

    // --- Dezinstalare -------------------------------------------------------------------------

    /// <summary>La dezinstalare se sterg si cererile, invitatiile si configuratia plugin-ului.</summary>
    public bool DeleteDataOnUninstall { get; set; }

    // --- Intern ------------------------------------------------------------------------------

    /// <summary>Cheia HMAC pentru tokenurile de formular; generata la prima pornire.</summary>
    public string FormSecret { get; set; } = string.Empty;
}

public static class RegistrationModes
{
    public const string Approval = "Approval";
    public const string Automatic = "Automatic";
    public const string InviteOnly = "InviteOnly";
}
