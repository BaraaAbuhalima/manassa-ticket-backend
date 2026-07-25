namespace manassa_ticket_backend.Services.Email;

public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string fromEmail,
        string subject,
        string body,
        string? attachmentPath = null,
        string? attachmentFileName = null,
        CancellationToken cancellationToken = default);
}