using System.Text.Json;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Logging;
using Registration.Security;
using Registration.Validation;

namespace Registration.Notifications;

public static class NotifyEvents
{
    public const string NewRequest = "NewRequest";
    public const string EmailConfirmed = "EmailConfirmed";
    public const string Decision = "Decision";
    public const string Abuse = "Abuse";
}

/// <summary>
/// Notificarile catre admin, pe canalele pornite in setari: jurnalul de activitate Emby
/// (imediat), e-mail si Telegram (imediat sau grupate la cateva minute).
/// </summary>
public sealed class AdminNotifier
{
    private readonly IActivityManager _activity;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private readonly List<string> _batch = new();
    private DateTimeOffset _lastFlush = DateTimeOffset.MinValue;

    public AdminNotifier(IActivityManager activity, ILogger logger)
    {
        _activity = activity;
        _logger = logger;
    }

    private static PluginConfiguration Settings => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public static bool Wants(PluginConfiguration settings, string kind) => kind switch
    {
        NotifyEvents.NewRequest => settings.NotifyOnNewRequest,
        NotifyEvents.EmailConfirmed => settings.NotifyOnEmailConfirmed,
        NotifyEvents.Decision => settings.NotifyOnDecision,
        NotifyEvents.Abuse => settings.NotifyOnAbuse,
        _ => true,
    };

    public void Notify(string kind, string title, string text, bool warning = false)
    {
        var settings = Settings;
        if (!Wants(settings, kind))
        {
            return;
        }

        if (settings.NotifyActivityLog)
        {
            try
            {
                _activity.Create(new ActivityLogEntry
                {
                    Name = title,
                    Overview = text,
                    ShortOverview = text.Length > 120 ? text[..117] + "..." : text,
                    Type = "Registration" + kind,
                    Date = DateTimeOffset.UtcNow,
                    Severity = warning ? LogSeverity.Warn : LogSeverity.Info,
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Inregistrare: nu am putut scrie in jurnalul de activitate", ex);
            }
        }

        if (!settings.NotifyEmail && !settings.NotifyTelegram)
        {
            return;
        }

        var message = title + "\n" + text;
        if (settings.NotifyBatchMinutes <= 0)
        {
            _ = SendAsync(settings, new[] { message });
            return;
        }

        lock (_lock)
        {
            _batch.Add(message);
        }
    }

    /// <summary>Apelat periodic: trimite mesajele stranse, daca a trecut intervalul.</summary>
    public void FlushIfDue(DateTimeOffset now)
    {
        var settings = Settings;
        string[] messages;
        lock (_lock)
        {
            if (_batch.Count == 0 || now - _lastFlush < TimeSpan.FromMinutes(Math.Max(1, settings.NotifyBatchMinutes)))
            {
                return;
            }

            messages = _batch.ToArray();
            _batch.Clear();
            _lastFlush = now;
        }

        _ = SendAsync(settings, messages);
    }

    private async Task SendAsync(PluginConfiguration settings, IReadOnlyList<string> messages)
    {
        var subject = messages.Count == 1 ? messages[0].Split('\n')[0] : $"Înregistrare: {messages.Count} notificări";
        var body = string.Join("\n\n", messages);

        if (settings.NotifyEmail)
        {
            try
            {
                await Mailer.SendAsync(settings, Validators.Lines(settings.NotifyEmailTo), subject, body, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Inregistrare: e-mailul catre admin nu a putut fi trimis", ex);
            }
        }

        if (settings.NotifyTelegram)
        {
            try
            {
                await SendTelegramAsync(settings, body, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Inregistrare: mesajul Telegram nu a putut fi trimis", ex);
            }
        }
    }

    public static async Task SendTelegramAsync(PluginConfiguration settings, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.TelegramBotToken) || string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            throw new InvalidOperationException("Tokenul botului sau chat id-ul Telegram lipsesc.");
        }

        var token = settings.TelegramBotToken.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d+:[A-Za-z0-9_-]+$"))
        {
            throw new InvalidOperationException("Tokenul botului Telegram nu are forma 123456:ABC...");
        }

        var payload = JsonSerializer.Serialize(new { chat_id = settings.TelegramChatId.Trim(), text, disable_web_page_preview = true });
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        using var response = await WebChecks.Http.PostAsync(
            $"https://api.telegram.org/bot{token}/sendMessage", content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Telegram a raspuns {(int)response.StatusCode}: {body}");
        }
    }
}
