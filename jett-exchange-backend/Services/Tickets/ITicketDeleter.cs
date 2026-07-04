using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Requests;

namespace jett_exchange_backend.Services.Tickets;

public interface ITicketDeleter
{
    Task<ApiResponse<string>> DeleteByIdAsync(Guid id);
    Task<ApiResponse<string>> DeleteByTokenAsync(string token);
    Task<ApiResponse<string>> RepublishByTokenAsync(string token);
    Task<ApiResponse<string>> ModifyTicketByTokenAsync(string token, UpdateTicketRequest request);
}
