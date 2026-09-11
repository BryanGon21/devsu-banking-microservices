using System.Text.Json;
using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Messaging.Inbox;

namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal static class CustomerIntegrationEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static CustomerIntegrationEventEnvelope Deserialize(
        ReadOnlySpan<byte> body,
        string routingKey)
    {
        try
        {
            CustomerIntegrationEventEnvelope? integrationEvent =
                JsonSerializer.Deserialize<CustomerIntegrationEventEnvelope>(body, SerializerOptions);

            if (integrationEvent is null)
            {
                throw new InvalidIntegrationEventException("Customer event body is empty.");
            }

            Validate(integrationEvent, routingKey);
            return integrationEvent;
        }
        catch (JsonException exception)
        {
            throw new InvalidIntegrationEventException("Customer event JSON is invalid.", exception);
        }
    }

    private static void Validate(
        CustomerIntegrationEventEnvelope integrationEvent,
        string routingKey)
    {
        if (integrationEvent.EventId == Guid.Empty ||
            integrationEvent.AggregateId == Guid.Empty ||
            integrationEvent.Data is null ||
            integrationEvent.Data.CustomerId == Guid.Empty)
        {
            throw new InvalidIntegrationEventException("Customer event identifiers are invalid.");
        }

        if (!CustomerIntegrationEventContract.IsSupported(integrationEvent.EventType) ||
            !string.Equals(integrationEvent.EventType, routingKey, StringComparison.Ordinal))
        {
            throw new InvalidIntegrationEventException("Customer event type and routing key are inconsistent.");
        }

        if (integrationEvent.AggregateId != integrationEvent.Data.CustomerId)
        {
            throw new InvalidIntegrationEventException("Customer aggregate and data identifiers do not match.");
        }

        if (integrationEvent.AggregateVersion < 1)
        {
            throw new InvalidIntegrationEventException("Customer aggregate version is invalid.");
        }

        if (integrationEvent.OccurredAtUtc == default ||
            integrationEvent.OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidIntegrationEventException("Customer event occurrence time is invalid.");
        }

        if (string.IsNullOrWhiteSpace(integrationEvent.CorrelationId) ||
            integrationEvent.CorrelationId.Length > InboxMessage.MaximumCorrelationIdLength)
        {
            throw new InvalidIntegrationEventException("Customer event correlation identifier is invalid.");
        }

        if (string.IsNullOrWhiteSpace(integrationEvent.Data.Name) ||
            integrationEvent.Data.Name.Trim().Length > CustomerProjection.MaximumNameLength)
        {
            throw new InvalidIntegrationEventException("Customer event name is invalid.");
        }

        if (integrationEvent.Data.IsActive && integrationEvent.Data.IsDeleted)
        {
            throw new InvalidIntegrationEventException("A deleted customer event cannot be active.");
        }

        bool isDeletionEvent = integrationEvent.EventType == CustomerIntegrationEventContract.DeletedV1;
        if (isDeletionEvent != integrationEvent.Data.IsDeleted)
        {
            throw new InvalidIntegrationEventException(
                "Customer event type and deletion state are inconsistent.");
        }
    }
}
