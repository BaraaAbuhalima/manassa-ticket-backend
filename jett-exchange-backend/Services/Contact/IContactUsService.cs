using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Requests;

namespace jett_exchange_backend.Services.Contact;

public interface IContactUsService
{
    Task<ApiResponse<string>> SubmitAsync(ContactUsRequest request, CancellationToken cancellationToken = default);
}
