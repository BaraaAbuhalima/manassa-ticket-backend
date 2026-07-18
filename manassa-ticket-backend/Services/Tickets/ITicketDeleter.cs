using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.DTOs.Responses;

namespace manassa_ticket_backend.Services.Tickets;

public interface ITicketDeleter
{
    Task<ApiResponse<string>> DeleteByIdAsync(Guid id);
    Task<ApiResponse<string>> DeleteByTokenAsync(string token);
    Task<ApiResponse<string>> RepublishByTokenAsync(string token);
    Task<ApiResponse<string>> ModifyTicketByTokenAsync(string token, UpdateTicketRequest request);
    Task<ApiResponse<TicketFileUrlResponse>> GetFileUrlByTokenAsync(string token);
}
