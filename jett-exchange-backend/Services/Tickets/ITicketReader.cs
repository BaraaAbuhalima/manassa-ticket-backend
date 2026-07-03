using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Responses;

namespace jett_exchange_backend.Services.Tickets;

public interface ITicketReader
{
    Task<ApiResponse<GetTicketByIdResponse>> GetByIdAsync(Guid id);
    Task<ApiResponse<GetTicketByPinResponse>> GetByPinAsync(string pin);
    Task<ApiResponse<List<GetTicketByIdResponse>>> GetForDateAsync(DateOnly date, int page);
    Task<ApiResponse<List<GetTicketByIdResponse>>> GetForDateRangeAsync(DateOnly startDate, DateOnly endDate, int page);
}
