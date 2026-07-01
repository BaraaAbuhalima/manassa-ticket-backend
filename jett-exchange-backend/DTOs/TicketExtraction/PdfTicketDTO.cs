namespace jett_exchange_backend.DTOs.TicketExtraction;

public class PdfTicketDTO
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public required string TicketId { get; set; }
    public required string BarCode { get; set; }
}