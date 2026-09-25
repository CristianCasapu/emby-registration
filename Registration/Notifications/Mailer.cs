using System.Net;
using System.Net.Mail;
using System.Text;

namespace Registration.Notifications;

/// <summary>
/// Trimitere prin SMTP cu STARTTLS (portul 587). System.Net.Mail nu stie TLS implicit
/// (portul 465); toti furnizorii mari accepta 587.
/// </summary>
public static class Mailer
{
    public static bool IsConfigured(PluginConfiguration settings) =>
        !string.IsNullOrWhiteSpace(settings.SmtpHost) && !string.IsNullOrWhiteSpace(settings.SmtpFrom);

    public static async Task SendAsync(PluginConfiguration settings, IEnumerable<string> to, string subject, string body, CancellationToken cancellationToken)
    {
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("SMTP nu este configurat.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.SmtpFrom),
            Subject = subject.Replace('\r', ' ').Replace('\n', ' '),
            Body = body,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };

        foreach (var address in to)
        {
            message.To.Add(new MailAddress(address));
        }

        if (message.To.Count == 0)
        {
            return;
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.SmtpStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20_000,
        };

        if (!string.IsNullOrEmpty(settings.SmtpUser))
        {
            client.Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword);
        }

        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
