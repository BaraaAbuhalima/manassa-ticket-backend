using System.Text.Json;
using manassa_ticket_backend.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace manassa_ticket_backend.Messaging;

public class RabbitMqTicketAvailablePublisher(RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options)
    : ITicketAvailablePublisher
{
    public async Task PublishAsync(TicketAvailableMessage message, CancellationToken cancellationToken = default)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.Value.TicketAvailableQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: options.Value.TicketAvailableQueueName,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
