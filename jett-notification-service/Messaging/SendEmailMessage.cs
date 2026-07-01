namespace jett_notification_service.Messaging;

public class SendEmailMessage
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
}
