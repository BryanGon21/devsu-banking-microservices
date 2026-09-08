using System.Diagnostics;
using System.Text.Json;
using Devsu.Customers.Application.IntegrationEvents;
using Devsu.Customers.Domain.Events;

namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal static class CustomerIntegrationEventMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static OutboxMessage Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent is not CustomerDomainEvent customerEvent)
        {
            throw new InvalidOperationException(
                $"Domain event '{domainEvent.GetType().Name}' does not have an integration mapping.");
        }

        string eventType = customerEvent switch
        {
            CustomerCreatedDomainEvent => CustomerIntegrationEventContract.CreatedV1,
            CustomerUpdatedDomainEvent => CustomerIntegrationEventContract.UpdatedV1,
            CustomerDeletedDomainEvent => CustomerIntegrationEventContract.DeletedV1,
            _ => throw new InvalidOperationException(
                $"Customer event '{customerEvent.GetType().Name}' does not have an integration mapping."),
        };

        string correlationId = Activity.Current?.TraceId.ToHexString()
            ?? customerEvent.EventId.ToString("N");

        CustomerIntegrationEventEnvelope envelope = new(
            customerEvent.EventId,
            eventType,
            customerEvent.OccurredAtUtc,
            correlationId,
            customerEvent.CustomerId,
            customerEvent.AggregateVersion,
            new CustomerIntegrationEventData(
                customerEvent.CustomerId,
                customerEvent.Name,
                customerEvent.IsActive,
                customerEvent.IsDeleted));

        string payload = JsonSerializer.Serialize(envelope, SerializerOptions);

        return OutboxMessage.Create(
            envelope.EventId,
            envelope.EventType,
            envelope.OccurredAtUtc,
            envelope.CorrelationId,
            envelope.AggregateId,
            envelope.AggregateVersion,
            payload);
    }
}
