using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Messaging;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Contact;

public class ContactUsService(
    IEmailMessagePublisher emailPublisher,
    IOptions<ContactOptions> contactOptions,
    IOptions<SmtpOptions> smtpOptions)
    : IContactUsService
{
    public async Task<ApiResponse<string>> SubmitAsync(ContactUsRequest request, CancellationToken cancellationToken = default)
    {
        await emailPublisher.PublishAsync(new SendEmailMessage
        {
            To = contactOptions.Value.RecipientEmail,
            From = smtpOptions.Value.FromAddress,
            Subject = $"Contact Us message from {request.Name} | رسالة تواصل من {request.Name}",
            // The body is the visitor's own message verbatim — not translated, since we have
            // no way to know what language it's already in or translate it accurately.
            Body = $"From: {request.Name} <{request.Email}>\n\n{request.Message}"
        }, cancellationToken);

        await emailPublisher.PublishAsync(new SendEmailMessage
        {
            To = request.Email,
            // Sent from the support inbox (not no-reply) since this is the one automated
            // email a customer might reasonably want to reply to.
            From = contactOptions.Value.RecipientEmail,
            Subject = "We've received your message - Manassa Ticket Exchange | استلمنا رسالتك",
            Body = $"""
                Hi {request.Name},

                Thanks for reaching out to Manassa Ticket Exchange. We've received your message and will get back to you soon.

                Your message:
                {request.Message}

                ---

                مرحبًا {request.Name}،

                شكرًا لتواصلك مع Manassa Ticket Exchange. لقد استلمنا رسالتك وسنتواصل معك قريبًا.

                رسالتك:
                {request.Message}
                """
        }, cancellationToken);

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Your message has been sent. We'll get back to you soon.",

        };
    }
}
