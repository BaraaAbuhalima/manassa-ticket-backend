using jett_exchange_backend.Common;

namespace jett_exchange_backend.Services.Tickets;

public interface ITicketDeleter
{
    Task<ApiResponse<string>> DeleteByIdAsync(Guid id);
    Task<ApiResponse<string>> DeleteByTokenAsync(string token);
}
