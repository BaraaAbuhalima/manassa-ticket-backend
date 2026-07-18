using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;

namespace manassa_ticket_backend.Services.Contact;

public interface IContactUsService
{
    Task<ApiResponse<string>> SubmitAsync(ContactUsRequest request, CancellationToken cancellationToken = default);
}
