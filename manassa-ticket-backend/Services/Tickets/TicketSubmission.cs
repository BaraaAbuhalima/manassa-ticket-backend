using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.Services.Tickets;

public class TicketSubmission
{
    public required Guid TicketRowId { get; set; }
    public required string SellerName { get; set; }
    public required string SellerEmail { get; set; }
    public required string SellerPhone { get; set; }
    public required decimal TotalPriceJod { get; set; }
    public required PaymentMethod PaymentMethod { get; set; }
    public required PaymentInfo PaymentInfo { get; set; }
    public required string Pin { get; set; }
    public required string TicketFilePath { get; set; }
}
