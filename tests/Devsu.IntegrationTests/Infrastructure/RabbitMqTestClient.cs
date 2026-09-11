using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Devsu.IntegrationTests.Infrastructure;

internal sealed class RabbitMqTestClient
{
    private readonly IntegrationTestFixture _fixture;

    public RabbitMqTestClient(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task WaitForQueueAsync(
        string queueName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using IConnection connection = await CreateConnectionAsync(cancellationToken);
                await using IChannel channel = await connection.CreateChannelAsync(
                    cancellationToken: cancellationToken);
                await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
                return;
            }
            catch (BrokerUnreachableException)
            {
            }
            catch (OperationInterruptedException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException($"RabbitMQ queue '{queueName}' was not declared in time.");
    }

    public async Task PublishAsync(
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        await using IConnection connection = await CreateConnectionAsync(cancellationToken);
        CreateChannelOptions options = new(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using IChannel channel = await connection.CreateChannelAsync(options, cancellationToken);
        await channel.ExchangeDeclareAsync(
            exchange: "customer.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);
        BasicProperties properties = new()
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            Persistent = true,
        };
        await channel.BasicPublishAsync(
            exchange: "customer.events",
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(payload),
            cancellationToken: cancellationToken);
    }

    private Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        ConnectionFactory connectionFactory = new()
        {
            HostName = _fixture.RabbitMqHostName,
            Port = _fixture.RabbitMqPort,
            UserName = _fixture.RabbitMqUser,
            Password = _fixture.RabbitMqSecret,
        };

        return connectionFactory.CreateConnectionAsync(
            "integration-test-client",
            cancellationToken);
    }
}
