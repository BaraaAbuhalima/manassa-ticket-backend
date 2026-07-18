using manassa_ticket_backend.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace manassa_ticket_backend.Messaging;

public class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionProvider> logger)
    : IAsyncDisposable
{
    private const int MaxConnectAttempts = 5;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // ConnectionFactory.Uri parses host/port/user/pass/vhost from the URL directly, and
            // enables TLS automatically for an "amqps://" scheme (vs. plain "amqp://").
            var factory = new ConnectionFactory
            {
                Uri = new Uri(options.Value.Url)
            };

            var delay = TimeSpan.FromSeconds(1);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                    return _connection;
                }
                catch (Exception ex) when (attempt < MaxConnectAttempts)
                {
                    logger.LogWarning(
                        ex,
                        "RabbitMQ connection attempt {Attempt}/{MaxAttempts} failed, retrying in {Delay}",
                        attempt, MaxConnectAttempts, delay);

                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
