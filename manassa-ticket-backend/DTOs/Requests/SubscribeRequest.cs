namespace manassa_ticket_backend.DTOs.Requests;

public class SubscribeRequest
{
    public required string Email { get; set; }
    public required DateOnly Date { get; set; }
}
