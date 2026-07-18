using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Messaging;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Contact;

public class ContactUsService(IEmailMessagePublisher emailPublisher, IOptions<ContactOptions> options) : IContactUsService
{
    public async Task<ApiResponse<string>> SubmitAsync(ContactUsRequest request, CancellationToken cancellationToken = default)
    {
        await emailPublisher.PublishAsync(new SendEmailMessage
        {
            To = options.Value.RecipientEmail,
            Subject = $"Contact Us message from {request.Name} | رسالة تواصل من {request.Name}",
            // The body is the visitor's own message verbatim — not translated, since we have
            // no way to know what language it's already in or translate it accurately.
            Body = $"From: {request.Name} <{request.Email}>\n\n{request.Message}"
        }, cancellationToken);

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Your message has been sent. We'll get back to you soon.",
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
            }
        };
    }
}
