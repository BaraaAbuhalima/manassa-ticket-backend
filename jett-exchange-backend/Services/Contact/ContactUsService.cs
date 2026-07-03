using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Messaging;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.Contact;

public class ContactUsService(IEmailMessagePublisher emailPublisher, IOptions<ContactOptions> options) : IContactUsService
{
    public async Task<ApiResponse<string>> SubmitAsync(ContactUsRequest request, CancellationToken cancellationToken = default)
    {
        await emailPublisher.PublishAsync(new SendEmailMessage
        {
            To = options.Value.RecipientEmail,
            Subject = $"Contact Us message from {request.Name}",
            Body = $"From: {request.Name} <{request.Email}>\n\n{request.Message}"
        }, cancellationToken);

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Your message has been sent. We'll get back to you soon."
        };
    }
}
