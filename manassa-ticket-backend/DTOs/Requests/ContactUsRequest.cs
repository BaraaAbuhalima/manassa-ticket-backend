namespace manassa_ticket_backend.DTOs.Requests;

public class ContactUsRequest
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Message { get; set; }
}
