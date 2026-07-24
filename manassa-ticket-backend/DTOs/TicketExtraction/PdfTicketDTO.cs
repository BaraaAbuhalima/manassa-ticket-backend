using System.Text.Json.Serialization;

namespace manassa_ticket_backend.DTOs.TicketExtraction;

public class PdfTicketDTO
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public required string TicketId { get; set; }
    public required string BarCode { get; set; }

    [JsonPropertyName("DateTime")]
    public DateTime? TicketDateTime { get; set; }
}