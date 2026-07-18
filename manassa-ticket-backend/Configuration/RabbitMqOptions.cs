namespace manassa_ticket_backend.Configuration;

public class RabbitMqOptions
{
    public string Url { get; set; } = string.Empty;
    public string TicketAvailableQueueName { get; set; } = string.Empty;
    public string EmailNotificationQueueName { get; set; } = string.Empty;
    public string TicketSoldNotificationQueueName { get; set; } = string.Empty;
    public string TicketPurchasedNotificationQueueName { get; set; } = string.Empty;
}
