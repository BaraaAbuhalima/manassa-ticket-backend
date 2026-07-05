using System.Text.Json;
using jett_exchange_backend.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace jett_exchange_backend.Messaging;

public class RabbitMqTicketSoldNotificationPublisher(RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options)
    : ITicketSoldNotificationPublisher
{
    public async Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.Value.TicketSoldNotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: options.Value.TicketSoldNotificationQueueName,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
