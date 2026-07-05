namespace jett_exchange_backend.Configuration;

public class RabbitMqOptions
{
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public bool UseTls { get; set; }
    public string TicketAvailableQueueName { get; set; } = string.Empty;
    public string EmailNotificationQueueName { get; set; } = string.Empty;
    public string TicketSoldNotificationQueueName { get; set; } = string.Empty;
    public string TicketPurchasedNotificationQueueName { get; set; } = string.Empty;
}
