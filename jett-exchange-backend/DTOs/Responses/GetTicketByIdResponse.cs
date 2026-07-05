namespace jett_exchange_backend.DTOs.Responses;

public class GetTicketByIdResponse
{
    public required Guid Id { get; set; }
    public required DateTime TicketDateTime { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPriceUsd { get; set; }
    public required decimal TotalPriceJod { get; set; }
}
