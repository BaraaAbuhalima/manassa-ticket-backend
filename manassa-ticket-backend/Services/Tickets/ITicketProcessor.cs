namespace manassa_ticket_backend.Services.Tickets;

public interface ITicketProcessor
{
    Task<TicketProcessingResult> ProcessAsync(TicketSubmission submission, CancellationToken cancellationToken = default);
}
