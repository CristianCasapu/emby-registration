using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaBrowser.Model.Logging;

namespace Registration.Security;

/// <summary>Verificari care cer un apel extern: Cloudflare Turnstile si Have I Been Pwned.</summary>
public sealed class WebChecks
{
    internal static readonly HttpClient Http = CreateClient();

    private readonly ILogger _logger;

    public WebChecks(ILogger logger)
    {
        _logger = logger;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("emby-registration-plugin");
        return client;
    }

    /// <summary>Verificarea server-side a raspunsului Turnstile (tokenul e valabil o singura data, 5 minute).</summary>
    public async Task<bool> VerifyTurnstileAsync(string secret, string? response, IPAddress? ip, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(response) || response.Length > 2048)
        {
            return false;
        }

        try
        {
            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = response };
            if (ip != null)
            {
                form["remoteip"] = RateLimiter.Normalize(ip)!.ToString();
            }

            using var result = await Http.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify", new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
            using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var success = json.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            if (!success && json.RootElement.TryGetProperty("error-codes", out var codes))
            {
                _logger.Info("Inregistrare: Turnstile a respins cererea: {0}", codes.ToString());
            }

            return success;
        }
        catch (Exception ex)
        {
            // Fara verificare nu se accepta cererea: mai bine un om reincearca decat sa treaca un robot.
            _logger.ErrorException("Inregistrare: verificarea Turnstile a esuat", ex);
            return false;
        }
    }

    /// <summary>
    /// De cate ori apare parola in scurgerile publice (k-anonimitate: pleaca doar primele 5
    /// caractere din SHA-1). Null daca serviciul nu raspunde; atunci nu blocam inregistrarea.
    /// </summary>
    public async Task<int?> PwnedCountAsync(string password, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.pwnedpasswords.com/range/" + hash[..5]);
            request.Headers.Add("Add-Padding", "true");
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return PwnedCount(body, hash[5..]);
        }
        catch (Exception ex)
        {
            _logger.Warn("Inregistrare: Have I Been Pwned nu raspunde ({0}); parola nu a fost verificata", ex.Message);
            return null;
        }
    }

    internal static int PwnedCount(string body, string suffix)
    {
        foreach (var line in body.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon == suffix.Length && line.AsSpan(0, colon).Equals(suffix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan(colon + 1).Trim(), out var count))
            {
                return count;
            }
        }

        return 0;
    }
}
