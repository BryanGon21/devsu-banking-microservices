using Devsu.Accounts.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal sealed class CustomerIntegrationEventConsumer : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly CustomerEventConsumerOptions _consumerOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CustomerIntegrationEventConsumer> _logger;

    public CustomerIntegrationEventConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<CustomerEventConsumerOptions> consumerOptions,
        TimeProvider timeProvider,
        ILogger<CustomerIntegrationEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Customer event consumer is unavailable. Retrying the connection.");

                try
                {
                    await Task.Delay(
                        _consumerOptions.ConnectionRetryDelay,
                        _timeProvider,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        ConnectionFactory connectionFactory = RabbitMqConnectionFactory.Create(_rabbitMqOptions);
        await using IConnection connection = await connectionFactory.CreateConnectionAsync(
            _rabbitMqOptions.ConnectionName,
            stoppingToken);
        CreateChannelOptions channelOptions = new(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true,
            consumerDispatchConcurrency: 1);
        await using IChannel channel = await connection.CreateChannelAsync(channelOptions, stoppingToken);
        TaskCompletionSource<bool> channelShutdown = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            channelShutdown.TrySetResult(true);
            return Task.CompletedTask;
        };

        await DeclareTopologyAsync(channel, stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: checked((ushort)_consumerOptions.PrefetchCount),
            global: false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += (_, eventArgs) =>
            HandleDeliveryAsync(channel, eventArgs, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: _consumerOptions.QueueName,
            autoAck: false,
            consumerTag: $"{_rabbitMqOptions.ConnectionName}-customer-events",
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Customer event consumer started on queue {QueueName}.",
            _consumerOptions.QueueName);

        await channelShutdown.Task.WaitAsync(stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken stoppingToken)
    {
        try
        {
            if (eventArgs.Body.Length > _consumerOptions.MaximumMessageBytes)
            {
                throw new InvalidIntegrationEventException("Customer event exceeds the allowed size.");
            }

            CustomerIntegrationEventEnvelope integrationEvent =
                CustomerIntegrationEventSerializer.Deserialize(eventArgs.Body.Span, eventArgs.RoutingKey);

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            CustomerIntegrationEventHandler handler =
                scope.ServiceProvider.GetRequiredService<CustomerIntegrationEventHandler>();
            CustomerEventProcessingResult result = await handler.HandleAsync(
                integrationEvent,
                stoppingToken);

            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Customer event {EventId} of type {EventType} completed with result {Result}.",
                integrationEvent.EventId,
                integrationEvent.EventType,
                result);
        }
        catch (InvalidIntegrationEventException exception)
        {
            _logger.LogWarning(
                exception,
                "Invalid customer event with delivery tag {DeliveryTag} was sent to the dead-letter queue.",
                eventArgs.DeliveryTag);
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The unacknowledged delivery will be returned to the queue when the channel closes.
        }
        catch (Exception exception)
        {
            await HandleTransientFailureAsync(channel, eventArgs, exception, stoppingToken);
        }
    }

    private async Task HandleTransientFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int retryCount = GetRetryCount(eventArgs.BasicProperties.Headers);
        if (retryCount >= _consumerOptions.MaximumRetries)
        {
            _logger.LogError(
                exception,
                "Customer event with delivery tag {DeliveryTag} exhausted {RetryCount} retries and was dead-lettered.",
                eventArgs.DeliveryTag,
                retryCount);
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: cancellationToken);
            return;
        }

        TimeSpan delay = CalculateRetryDelay(retryCount);
        await Task.Delay(delay, _timeProvider, cancellationToken);

        try
        {
            BasicProperties retryProperties = CopyProperties(eventArgs.BasicProperties, retryCount + 1);
            await channel.BasicPublishAsync(
                exchange: eventArgs.Exchange,
                routingKey: eventArgs.RoutingKey,
                mandatory: true,
                basicProperties: retryProperties,
                body: eventArgs.Body,
                cancellationToken: cancellationToken);
            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            _logger.LogWarning(
                exception,
                "Customer event with delivery tag {DeliveryTag} was republished for retry {RetryCount}.",
                eventArgs.DeliveryTag,
                retryCount + 1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception publishException)
        {
            _logger.LogError(
                publishException,
                "Could not republish customer event with delivery tag {DeliveryTag}; the original will be requeued.",
                eventArgs.DeliveryTag);
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken: cancellationToken);
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: CustomerIntegrationEventContract.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            exchange: _consumerOptions.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            queue: _consumerOptions.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: _consumerOptions.DeadLetterQueue,
            exchange: _consumerOptions.DeadLetterExchange,
            routingKey: _consumerOptions.DeadLetterQueue,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);

        Dictionary<string, object?> queueArguments = new()
        {
            ["x-dead-letter-exchange"] = _consumerOptions.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = _consumerOptions.DeadLetterQueue,
        };
        await channel.QueueDeclareAsync(
            queue: _consumerOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: _consumerOptions.QueueName,
            exchange: CustomerIntegrationEventContract.ExchangeName,
            routingKey: "customer.*.v1",
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        int exponent = Math.Min(retryCount, 30);
        double delayMilliseconds =
            _consumerOptions.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, exponent);

        return TimeSpan.FromMilliseconds(Math.Min(
            delayMilliseconds,
            _consumerOptions.MaximumRetryDelay.TotalMilliseconds));
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out object? value))
        {
            return 0;
        }

        return value switch
        {
            byte byteValue => byteValue,
            short shortValue when shortValue >= 0 => shortValue,
            int intValue when intValue >= 0 => intValue,
            long longValue when longValue is >= 0 and <= int.MaxValue => (int)longValue,
            byte[] bytes when int.TryParse(
                System.Text.Encoding.UTF8.GetString(bytes),
                out int parsedValue) && parsedValue >= 0 => parsedValue,
            _ => 0,
        };
    }

    private static BasicProperties CopyProperties(
        IReadOnlyBasicProperties source,
        int retryCount)
    {
        Dictionary<string, object?> headers = source.Headers is null
            ? []
            : new Dictionary<string, object?>(source.Headers, StringComparer.Ordinal);
        headers[RetryCountHeader] = retryCount;

        return new BasicProperties
        {
            ContentType = source.ContentType,
            ContentEncoding = source.ContentEncoding,
            CorrelationId = source.CorrelationId,
            MessageId = source.MessageId,
            Type = source.Type,
            Timestamp = source.Timestamp,
            Persistent = true,
            Headers = headers,
        };
    }
}
