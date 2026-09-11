using System.Text.Json.Serialization;

namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal static class CustomerIntegrationEventContract
{
    public const string ExchangeName = "customer.events";
    public const string CreatedV1 = "customer.created.v1";
    public const string UpdatedV1 = "customer.updated.v1";
    public const string DeletedV1 = "customer.deleted.v1";

    public static bool IsSupported(string eventType)
    {
        return eventType is CreatedV1 or UpdatedV1 or DeletedV1;
    }
}

internal sealed record CustomerIntegrationEventEnvelope(
    [property: JsonRequired, JsonPropertyName("eventId")] Guid EventId,
    [property: JsonRequired, JsonPropertyName("eventType")] string EventType,
    [property: JsonRequired, JsonPropertyName("occurredAtUtc")] DateTimeOffset OccurredAtUtc,
    [property: JsonRequired, JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonRequired, JsonPropertyName("aggregateId")] Guid AggregateId,
    [property: JsonRequired, JsonPropertyName("aggregateVersion")] long AggregateVersion,
    [property: JsonRequired, JsonPropertyName("data")] CustomerIntegrationEventData Data);

internal sealed record CustomerIntegrationEventData(
    [property: JsonRequired, JsonPropertyName("customerId")] Guid CustomerId,
    [property: JsonRequired, JsonPropertyName("name")] string Name,
    [property: JsonRequired, JsonPropertyName("isActive")] bool IsActive,
    [property: JsonRequired, JsonPropertyName("isDeleted")] bool IsDeleted);
