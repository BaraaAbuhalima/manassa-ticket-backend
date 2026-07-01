using System.Net;
using System.Net.Mail;
using jett_notification_service.Configuration;
using Microsoft.Extensions.Options;

namespace jett_notification_service.Services;

public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(options.Value.Host, options.Value.Port)
        {
            EnableSsl = options.Value.EnableSsl
        };

        if (!string.IsNullOrEmpty(options.Value.Username))
        {
            client.Credentials = new NetworkCredential(options.Value.Username, options.Value.Password);
        }

        using var message = new MailMessage(options.Value.FromAddress, toEmail, subject, body);
        await client.SendMailAsync(message, cancellationToken);

        logger.LogInformation("Sent email to {ToEmail} with subject '{Subject}'", toEmail, subject);
    }
}
