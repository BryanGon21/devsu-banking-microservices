using System.Text;
using Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

namespace Devsu.Accounts.UnitTests.Infrastructure;

public sealed class CustomerIntegrationEventSerializerTests
{
    [Fact]
    public void Deserialize_WithValidContract_ReturnsEnvelope()
    {
        Guid eventId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        string json = CreateEventJson(eventId, customerId, aggregateVersion: 2);

        CustomerIntegrationEventEnvelope envelope = CustomerIntegrationEventSerializer.Deserialize(
            Encoding.UTF8.GetBytes(json),
            CustomerIntegrationEventContract.UpdatedV1);

        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal(customerId, envelope.AggregateId);
        Assert.Equal(customerId, envelope.Data.CustomerId);
        Assert.Equal(2, envelope.AggregateVersion);
        Assert.Equal("Jose Lema", envelope.Data.Name);
    }

    [Fact]
    public void Deserialize_WithUnknownProperty_RejectsContractDrift()
    {
        string json = CreateEventJson(Guid.NewGuid(), Guid.NewGuid(), 1)
            .Replace("\"data\":", "\"unexpected\":true,\"data\":", StringComparison.Ordinal);

        InvalidIntegrationEventException exception = Assert.Throws<InvalidIntegrationEventException>(() =>
            CustomerIntegrationEventSerializer.Deserialize(
                Encoding.UTF8.GetBytes(json),
                CustomerIntegrationEventContract.UpdatedV1));

        Assert.Equal("Customer event JSON is invalid.", exception.Message);
    }

    [Fact]
    public void Deserialize_WithMismatchedRoutingKey_RejectsMessage()
    {
        string json = CreateEventJson(Guid.NewGuid(), Guid.NewGuid(), 1);

        InvalidIntegrationEventException exception = Assert.Throws<InvalidIntegrationEventException>(() =>
            CustomerIntegrationEventSerializer.Deserialize(
                Encoding.UTF8.GetBytes(json),
                CustomerIntegrationEventContract.DeletedV1));

        Assert.Contains("routing key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithoutData_ReturnsControlledContractError()
    {
        Guid eventId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        string json = $$"""
            {
              "eventId":"{{eventId}}",
              "eventType":"customer.updated.v1",
              "occurredAtUtc":"2026-02-01T10:00:00Z",
              "correlationId":"correlation",
              "aggregateId":"{{customerId}}",
              "aggregateVersion":2,
              "data":null
            }
            """;

        InvalidIntegrationEventException exception = Assert.Throws<InvalidIntegrationEventException>(() =>
            CustomerIntegrationEventSerializer.Deserialize(
                Encoding.UTF8.GetBytes(json),
                CustomerIntegrationEventContract.UpdatedV1));

        Assert.Contains("identifiers", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateEventJson(Guid eventId, Guid customerId, long aggregateVersion)
    {
        return $$"""
            {
              "eventId":"{{eventId}}",
              "eventType":"customer.updated.v1",
              "occurredAtUtc":"2026-02-01T10:00:00Z",
              "correlationId":"correlation",
              "aggregateId":"{{customerId}}",
              "aggregateVersion":{{aggregateVersion}},
              "data":{
                "customerId":"{{customerId}}",
                "name":"Jose Lema",
                "isActive":true,
                "isDeleted":false
              }
            }
            """;
    }
}
