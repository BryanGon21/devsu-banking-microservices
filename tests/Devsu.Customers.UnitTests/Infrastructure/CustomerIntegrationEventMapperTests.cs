using System.Text.Json;
using Devsu.Customers.Application.IntegrationEvents;
using Devsu.Customers.Domain.Events;
using Devsu.Customers.Infrastructure.Messaging.Outbox;

namespace Devsu.Customers.UnitTests.Infrastructure;

public sealed class CustomerIntegrationEventMapperTests
{
    [Fact]
    public void Map_CreatedEvent_ProducesVersionedWireContractWithoutSensitiveData()
    {
        Guid eventId = Guid.Parse("53e4cd9d-6199-4a9c-a2e6-0dcfe72cbe07");
        Guid customerId = Guid.Parse("48d8eeef-3df6-45de-a3cc-6d4913271c1c");
        DateTimeOffset occurredAtUtc = new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);
        CustomerCreatedDomainEvent domainEvent = new(
            eventId,
            occurredAtUtc,
            customerId,
            1,
            "Jose Lema",
            true,
            false);

        OutboxMessage message = CustomerIntegrationEventMapper.Map(domainEvent);

        Assert.Equal(CustomerIntegrationEventContract.CreatedV1, message.EventType);
        Assert.Equal(eventId, message.EventId);
        Assert.Equal(customerId, message.AggregateId);
        Assert.Equal(1, message.AggregateVersion);
        Assert.Equal(occurredAtUtc, message.OccurredAtUtc);

        using JsonDocument document = JsonDocument.Parse(message.Payload);
        JsonElement root = document.RootElement;
        Assert.Equal(eventId, root.GetProperty("eventId").GetGuid());
        Assert.Equal("customer.created.v1", root.GetProperty("eventType").GetString());
        Assert.Equal(customerId, root.GetProperty("aggregateId").GetGuid());
        Assert.Equal(1, root.GetProperty("aggregateVersion").GetInt64());

        JsonElement data = root.GetProperty("data");
        Assert.Equal(customerId, data.GetProperty("customerId").GetGuid());
        Assert.Equal("Jose Lema", data.GetProperty("name").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.False(data.GetProperty("isDeleted").GetBoolean());

        string[] sensitiveProperties =
        [
            "password",
            "passwordHash",
            "identification",
            "address",
            "phone",
            "contrasena",
            "identificacion",
            "direccion",
            "telefono",
        ];
        foreach (string property in sensitiveProperties)
        {
            Assert.DoesNotContain($"\"{property}\"", message.Payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(false, CustomerIntegrationEventContract.UpdatedV1)]
    [InlineData(true, CustomerIntegrationEventContract.DeletedV1)]
    public void Map_ChangeEvent_UsesExpectedEventType(bool isDeleted, string expectedEventType)
    {
        Guid eventId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        CustomerDomainEvent domainEvent = isDeleted
            ? new CustomerDeletedDomainEvent(
                eventId,
                DateTimeOffset.UtcNow,
                customerId,
                2,
                "Jose Lema",
                false,
                true)
            : new CustomerUpdatedDomainEvent(
                eventId,
                DateTimeOffset.UtcNow,
                customerId,
                2,
                "Jose Lema",
                false,
                false);

        OutboxMessage message = CustomerIntegrationEventMapper.Map(domainEvent);

        Assert.Equal(expectedEventType, message.EventType);
    }
}
