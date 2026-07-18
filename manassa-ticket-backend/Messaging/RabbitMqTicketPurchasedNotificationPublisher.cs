using System.Text.Json;
using manassa_ticket_backend.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace manassa_ticket_backend.Messaging;

public class RabbitMqTicketPurchasedNotificationPublisher(RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options)
    : ITicketPurchasedNotificationPublisher
{
    public async Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.Value.TicketPurchasedNotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: options.Value.TicketPurchasedNotificationQueueName,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
