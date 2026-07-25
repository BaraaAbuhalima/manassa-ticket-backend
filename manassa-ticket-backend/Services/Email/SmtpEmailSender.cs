using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using manassa_ticket_backend.Configuration;

using manassa_ticket_backend.Services.FileStorage;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Email;

public class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    IOptions<ContactOptions> contactOptions,
    IFileStorage fileStorage,
    ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(
        string toEmail,
        string fromEmail,
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

        // The support mailbox (Contact:RecipientEmail) is a separate Zoho login from the
        // default no-reply sender, so it needs its own credentials to authenticate as itself.
        var (username, password) = string.Equals(fromEmail, contactOptions.Value.RecipientEmail, StringComparison.OrdinalIgnoreCase)
            ? (contactOptions.Value.SmtpUsername, contactOptions.Value.SmtpPassword)
            : (options.Value.Username, options.Value.Password);

        if (!string.IsNullOrEmpty(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var message = new MailMessage(fromEmail, toEmail, subject, body);

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