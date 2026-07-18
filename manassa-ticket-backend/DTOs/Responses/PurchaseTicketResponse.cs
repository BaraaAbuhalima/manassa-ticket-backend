namespace manassa_ticket_backend.DTOs.Responses;

public class PurchaseTicketResponse
{
    public required Guid TicketId { get; set; }
    public required string ClientSecret { get; set; }
    public required string PublishableKey { get; set; }
    public required decimal TicketPrice { get; set; }
    public required decimal Fee { get; set; }
    // TicketPrice + Fee — the actual amount charged via Stripe, not just the listed price.
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
}
