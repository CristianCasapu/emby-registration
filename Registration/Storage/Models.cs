namespace Registration.Storage;

public static class RequestStatus
{
    /// <summary>Asteapta confirmarea adresei de e-mail.</summary>
    public const string EmailPending = "EmailPending";

    /// <summary>Asteapta aprobarea adminului.</summary>
    public const string Pending = "Pending";

    public const string Approved = "Approved";

    public const string Rejected = "Rejected";

    /// <summary>Nu a fost confirmata sau aprobata la timp; contul dezactivat a fost sters.</summary>
    public const string Expired = "Expired";

    /// <summary>Contul Emby a fost sters ulterior (din Emby sau din plugin).</summary>
    public const string Deleted = "Deleted";

    public static bool IsWaiting(string status) => status is EmailPending or Pending;
}

/// <summary>O cerere de cont si datele pe care Emby nu le are (nume, e-mail, telefon, proveniente).</summary>
public sealed class RegistrationRecord
{
    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = RequestStatus.Pending;

    /// <summary>Contul Emby (Guid, format N). Contul exista inca din momentul cererii, dezactivat.</summary>
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool HasPin { get; set; }

    public string? InviteCode { get; set; }

    public string Ip { get; set; } = string.Empty;

    /// <summary>Tara vazuta de Cloudflare (antetul CF-IPCountry), daca exista.</summary>
    public string? IpCountry { get; set; }

    public string? UserAgent { get; set; }

    public string Language { get; set; } = "ro";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ConsentAt { get; set; }

    public DateTimeOffset? EmailConfirmedAt { get; set; }

    /// <summary>SHA-256 al tokenului din linkul de confirmare; tokenul insusi nu se pastreaza.</summary>
    public string? EmailTokenHash { get; set; }

    public DateTimeOffset? EmailTokenExpires { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? DecidedBy { get; set; }

    public string? Reason { get; set; }

    /// <summary>Contul se dezactiveaza singur atunci (cont de proba); null = niciodata.</summary>
    public DateTimeOffset? AccountExpiresAt { get; set; }

    /// <summary>Reteaua (/24 IPv4, /64 IPv6), pentru regula „o cerere per adresa”.</summary>
    public string? Subnet { get; set; }

    /// <summary>Hash-ul identificatorului de dispozitiv (nu identificatorul insusi).</summary>
    public string? DeviceHash { get; set; }

    /// <summary>Amprenta browserului.</summary>
    public string? Fingerprint { get; set; }

    public long? Asn { get; set; }

    /// <summary>Furnizorul de internet (organizatia ASN).</summary>
    public string? NetworkName { get; set; }

    /// <summary>residential, warp, local, unknown (centrele de date si Tor sunt refuzate).</summary>
    public string? NetworkKind { get; set; }

    /// <summary>Semne de cont dublu, semnalate adminului (nu blocate).</summary>
    public List<string> Flags { get; set; } = new();

    /// <summary>Paznicul de politica pastreaza drepturile minime; adminul poate „elibera” contul.</summary>
    public bool Managed { get; set; } = true;
}

public sealed class InviteCode
{
    public string Code { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>0 = nelimitat.</summary>
    public int MaxUses { get; set; } = 1;

    public int Uses { get; set; }

    public bool Revoked { get; set; }

    public bool IsUsable(DateTimeOffset now) =>
        !Revoked && (ExpiresAt == null || ExpiresAt > now) && (MaxUses <= 0 || Uses < MaxUses);
}

/// <summary>Contoare pe zi (UTC): cereri primite si motivele pentru care au fost oprite.</summary>
public sealed class DailyStats
{
    public string Day { get; set; } = string.Empty;

    public Dictionary<string, int> Counters { get; set; } = new();
}

/// <summary>Blocare dupa coduri de acces gresite: separat pe adresa IP si pe dispozitiv.</summary>
public sealed class AccessBlock
{
    public string Id { get; set; } = string.Empty;

    /// <summary>ip sau device.</summary>
    public string Kind { get; set; } = "ip";

    /// <summary>Adresa IP sau hash-ul dispozitivului.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Pentru admin: adresa, tara, browserul.</summary>
    public string? Label { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset Until { get; set; }

    public int Attempts { get; set; }

    public bool IsActive(DateTimeOffset now) => Until > now;
}

/// <summary>O incercare gresita de cod, pastrata 24 de ore.</summary>
public sealed class CodeAttempt
{
    public DateTimeOffset At { get; set; }

    public string Ip { get; set; } = string.Empty;

    public string? DeviceHash { get; set; }
}

/// <summary>Folosirea unui cod (cine a deblocat formularul).</summary>
public sealed class CodeUse
{
    public string Code { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public string? UsedByIp { get; set; }

    public string? UsedByCountry { get; set; }

    /// <summary>Cod inlocuit manual de admin, nefolosit.</summary>
    public bool Replaced { get; set; }
}

public sealed class StoreData
{
    /// <summary>Codul de acces valabil acum (unul singur).</summary>
    public string? AccessCode { get; set; }

    public DateTimeOffset? AccessCodeCreated { get; set; }

    /// <summary>Toate codurile emise vreodata: un cod nu se repeta.</summary>
    public List<CodeUse> CodeHistory { get; set; } = new();

    public List<AccessBlock> Blocks { get; set; } = new();

    public List<CodeAttempt> CodeAttempts { get; set; } = new();

    public List<RegistrationRecord> Requests { get; set; } = new();

    public List<InviteCode> Invites { get; set; } = new();

    public List<DailyStats> Stats { get; set; } = new();
}
