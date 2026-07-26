using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Responses;

namespace manassa_ticket_backend.Services.Tickets;

public interface ITicketReader
{
    Task<ApiResponse<GetTicketByIdResponse>> GetByIdAsync(Guid id);
    Task<ApiResponse<GetTicketByPinResponse>> GetByPinAsync(string pin, string email);
    Task<ApiResponse<List<GetTicketByIdResponse>>> GetForDateAsync(DateOnly date, int page);
    Task<ApiResponse<List<GetTicketByIdResponse>>> GetForDateRangeAsync(DateOnly startDate, DateOnly endDate, int page);
    Task<ApiResponse<List<GetTicketByIdResponse>>> GetCheapestForNextTwoWeeksAsync();
}
