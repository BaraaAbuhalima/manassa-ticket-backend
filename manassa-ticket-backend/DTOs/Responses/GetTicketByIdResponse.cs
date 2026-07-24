namespace manassa_ticket_backend.DTOs.Responses;

public class GetTicketByIdResponse
{
    public required Guid Id { get; set; }
    public DateTime? TicketDateTime { get; set; }
    public int? NumberOfBags { get; set; }
    public required decimal TotalPriceUsd { get; set; }
    public required decimal TotalPriceJod { get; set; }
}
