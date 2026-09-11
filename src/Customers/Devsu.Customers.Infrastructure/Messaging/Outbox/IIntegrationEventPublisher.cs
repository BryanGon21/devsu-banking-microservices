namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal interface IIntegrationEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
