using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using jett_exchange_backend.Configuration;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.Email;

public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        string? attachmentPath = null,
        string? attachmentFileName = null,
        CancellationToken cancellationToken = default)
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

        if (attachmentPath is not null)
        {
            if (File.Exists(attachmentPath))
            {
                var attachment = new Attachment(attachmentPath, MediaTypeNames.Application.Pdf);
                if (attachmentFileName is not null)
                {
                    attachment.Name = attachmentFileName;
                }

                message.Attachments.Add(attachment);
            }
            else
            {
                logger.LogWarning(
                    "Attachment file {AttachmentPath} not found; sending email to {ToEmail} without it",
                    attachmentPath, toEmail);
            }
        }

        await client.SendMailAsync(message, cancellationToken);

        logger.LogInformation("Sent email to {ToEmail} with subject '{Subject}'", toEmail, subject);
    }
}