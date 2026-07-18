using manassa_ticket_backend.DTOs.TicketExtraction;

namespace manassa_ticket_backend.Services.TicketExtraction;

public interface ITicketDataExtractor
{
    Task<PdfTicketDTO> ExtractTicketAsync(Stream fileStream, string fileName);
}