namespace manassa_ticket_backend.Messaging;

public class SendEmailMessage
{
    public required string To { get; set; }
    public required string From { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
}
