namespace Registration.Flow;

/// <summary>Textele e-mailurilor catre utilizatori, in romana si engleza.</summary>
public static class Texts
{
    public sealed record Mail(string Subject, string Body);

    private static bool En(string? language) => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

    public static Mail ConfirmEmail(string? language, string server, string firstName, string link, int hours) => En(language)
        ? new($"{server}: confirm your email address",
            $"Hello {firstName},\n\nTo continue your account request on {server}, confirm your email address by opening this link (valid for {hours} hours):\n\n{link}\n\nIf you did not request an account, ignore this message.")
        : new($"{server}: confirmă adresa de e-mail",
            $"Bună, {firstName},\n\nCa să continui cererea de cont pe {server}, confirmă adresa de e-mail deschizând linkul de mai jos (valabil {hours} ore):\n\n{link}\n\nDacă nu ai cerut tu un cont, ignoră acest mesaj.");

    public static Mail Received(string? language, string server, string firstName, string username) => En(language)
        ? new($"{server}: account request received",
            $"Hello {firstName},\n\nWe received your request for the account \"{username}\" on {server}. You will get an email as soon as it is approved.")
        : new($"{server}: am primit cererea de cont",
            $"Bună, {firstName},\n\nAm primit cererea pentru contul „{username}” pe {server}. Vei primi un e-mail imediat ce este aprobată.");

    public static Mail Approved(string? language, string server, string firstName, string username, string? url) => En(language)
        ? new($"{server}: your account is ready",
            $"Hello {firstName},\n\nYour account \"{username}\" on {server} has been approved. Sign in with the username and password you chose."
            + (url == null ? string.Empty : $"\n\nIn a browser: {url}\nIn the Emby apps (Android, iOS, TV), add the server manually using this address.")
            + "\n\nThe account is not shown on the sign-in screen: type your username.")
        : new($"{server}: contul tău este gata",
            $"Bună, {firstName},\n\nContul „{username}” pe {server} a fost aprobat. Intră cu numele de utilizator și parola alese la înregistrare."
            + (url == null ? string.Empty : $"\n\nÎn browser: {url}\nÎn aplicațiile Emby (Android, iOS, TV), adaugă serverul manual cu această adresă.")
            + "\n\nContul nu apare pe ecranul de conectare: scrie numele de utilizator.");

    public static Mail Rejected(string? language, string server, string firstName, string? reason) => En(language)
        ? new($"{server}: account request declined",
            $"Hello {firstName},\n\nYour account request on {server} was declined." + (string.IsNullOrWhiteSpace(reason) ? string.Empty : $"\n\nReason: {reason}"))
        : new($"{server}: cererea de cont a fost respinsă",
            $"Bună, {firstName},\n\nCererea ta de cont pe {server} a fost respinsă." + (string.IsNullOrWhiteSpace(reason) ? string.Empty : $"\n\nMotiv: {reason}"));
}
