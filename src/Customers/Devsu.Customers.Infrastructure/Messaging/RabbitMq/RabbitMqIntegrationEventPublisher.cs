using System.Text;
using Devsu.Customers.Application.IntegrationEvents;
using Devsu.Customers.Infrastructure.Messaging.Outbox;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Devsu.Customers.Infrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqIntegrationEventPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            IChannel channel = await GetOrCreateChannelAsync(cancellationToken);
            BasicProperties properties = new()
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                Persistent = true,
                MessageId = message.EventId.ToString("D"),
                CorrelationId = message.CorrelationId,
                Type = message.EventType,
                Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds()),
            };
            ReadOnlyMemory<byte> body = Encoding.UTF8.GetBytes(message.Payload);

            await channel.BasicPublishAsync(
                exchange: CustomerIntegrationEventContract.ExchangeName,
                routingKey: message.EventType,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch
        {
            await DisposeConnectionAsync();
            throw;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _channelLock.WaitAsync();
        try
        {
            await DisposeConnectionAsync();
        }
        finally
        {
            _channelLock.Release();
        }

        _channelLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return _channel;
        }

        await DisposeConnectionAsync();

        ConnectionFactory connectionFactory = RabbitMqConnectionFactory.Create(_options);
        _connection = await connectionFactory.CreateConnectionAsync(
            _options.ConnectionName,
            cancellationToken);

        CreateChannelOptions channelOptions = new(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);
        await _channel.ExchangeDeclareAsync(
            exchange: CustomerIntegrationEventContract.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        return _channel;
    }

    private async Task DisposeConnectionAsync()
    {
        IChannel? channel = _channel;
        IConnection? connection = _connection;
        _channel = null;
        _connection = null;

        try
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
    }
}
