using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using jett_exchange_backend.Configuration;

using jett_exchange_backend.Services.FileStorage;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.Email;

public class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    IFileStorage fileStorage,
    ILogger<SmtpEmailSender> logger)
    : IEmailSender
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

        Stream? attachmentStream = null;
        if (attachmentPath is not null)
        {
            attachmentStream = await fileStorage.OpenReadAsync(attachmentPath);
            if (attachmentStream is not null)
            {
                var attachment = new Attachment(attachmentStream, MediaTypeNames.Application.Pdf);
                if (attachmentFileName is not null)
                {
                    attachment.Name = attachmentFileName;
                }

                message.Attachments.Add(attachment);
            }
            else
            {
                logger.LogWarning(
                    "Attachment {AttachmentPath} not found in storage; sending email to {ToEmail} without it",
                    attachmentPath, toEmail);
            }
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        finally
        {
            if (attachmentStream is not null)
            {
                await attachmentStream.DisposeAsync();
            }
        }

        logger.LogInformation("Sent email to {ToEmail} with subject '{Subject}'", toEmail, subject);
    }
}