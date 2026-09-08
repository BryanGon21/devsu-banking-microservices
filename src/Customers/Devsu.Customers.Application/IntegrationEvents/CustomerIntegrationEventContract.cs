using System.Text.Json.Serialization;

namespace Devsu.Customers.Application.IntegrationEvents;

public static class CustomerIntegrationEventContract
{
    public const string ExchangeName = "customer.events";
    public const string CreatedV1 = "customer.created.v1";
    public const string UpdatedV1 = "customer.updated.v1";
    public const string DeletedV1 = "customer.deleted.v1";
}

public sealed record CustomerIntegrationEventEnvelope(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("occurredAtUtc")] DateTimeOffset OccurredAtUtc,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("aggregateId")] Guid AggregateId,
    [property: JsonPropertyName("aggregateVersion")] long AggregateVersion,
    [property: JsonPropertyName("data")] CustomerIntegrationEventData Data);

public sealed record CustomerIntegrationEventData(
    [property: JsonPropertyName("customerId")] Guid CustomerId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("isDeleted")] bool IsDeleted);
