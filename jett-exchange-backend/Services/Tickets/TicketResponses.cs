using jett_exchange_backend.Common;

namespace jett_exchange_backend.Services.Tickets;

internal static class TicketResponses
{
    public static ApiResponse<T> NotFound<T>()
    {
        return new ApiResponse<T>
        {
            StatusCode = StatusCodes.Status404NotFound,
            Success = false,
            Message = "Ticket not found",
            Errors = ["Ticket not found"],
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
            }
        };
    }
}
