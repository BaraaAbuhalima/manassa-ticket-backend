namespace manassa_ticket_backend.DTOs.Responses;

public class GetTicketByIdResponse
{
    public required Guid Id { get; set; }
    public DateTime? TicketDateTime { get; set; }
    public int? NumberOfBags { get; set; }
    public required decimal TotalPriceUsd { get; set; }
    public required decimal TotalPriceJod { get; set; }
    public required decimal Fee { get; set; }
    // TotalPriceUsd + Fee — the actual amount a buyer pays at checkout, not just the listed price.
    public required decimal Amount { get; set; }
    // Amount converted to JOD, for display alongside TotalPriceJod — computed server-side so the
    // frontend never has to derive money figures from a client-side exchange rate.
    public required decimal AmountJod { get; set; }
}
