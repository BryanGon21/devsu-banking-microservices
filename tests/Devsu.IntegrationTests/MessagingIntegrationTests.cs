using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Persistence;
using Devsu.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Devsu.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class MessagingIntegrationTests : IAsyncLifetime
{
    private static readonly TimeSpan EventuallyTimeout = TimeSpan.FromSeconds(15);

    private readonly IntegrationTestFixture _fixture;

    public MessagingIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetCustomersDatabaseAsync();
        await _fixture.ResetAccountsDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CustomerCreatedEvent_FlowsFromOutboxToLocalProjection()
    {
        string queueName = $"accounts.customer-events.{Guid.NewGuid():N}";
        await using AccountsApiFactory accountsFactory = new(
            _fixture,
            enableMessaging: true,
            queueName);
        using HttpClient accountsClient = accountsFactory.CreateClient();
        using HttpResponseMessage liveResponse = await accountsClient.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        RabbitMqTestClient rabbitMq = new(_fixture);
        await rabbitMq.WaitForQueueAsync(queueName, EventuallyTimeout);

        await using CustomersApiFactory customersFactory = new(
            _fixture,
            enableMessaging: true);
        using HttpClient customersClient = customersFactory.CreateClient();
        using HttpResponseMessage customerResponse = await customersClient.PostAsJsonAsync(
            "/api/clientes",
            new
            {
                nombre = "Projected Customer",
                genero = "Femenino",
                edad = 31,
                identificacion = "1919191919",
                direccion = "Integration Address",
                telefono = "0988888888",
                contrasena = "1234",
                estado = true,
            });
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        using JsonDocument createdCustomer = await ApiTestData.ReadJsonAsync(customerResponse);
        Guid customerId = createdCustomer.RootElement.GetProperty("clienteId").GetGuid();

        bool projected = await WaitUntilAsync(async () =>
        {
            await using AsyncServiceScope scope = accountsFactory.Services.CreateAsyncScope();
            AccountsDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
            return await dbContext.CustomerProjections.AsNoTracking().AnyAsync(
                customer => customer.CustomerId == customerId);
        });

        Assert.True(projected, "The customer event was not projected before the timeout.");
        Assert.Equal(1, await _fixture.CountPublishedOutboxMessagesAsync());
        Assert.Equal(1, await _fixture.CountInboxMessagesAsync());

        await using AsyncServiceScope assertionScope =
            accountsFactory.Services.CreateAsyncScope();
        AccountsDbContext assertionDbContext =
            assertionScope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        CustomerProjection projection = await assertionDbContext.CustomerProjections
            .AsNoTracking()
            .SingleAsync(customer => customer.CustomerId == customerId);
        Assert.Equal("Projected Customer", projection.Name);
        Assert.True(projection.IsAvailable);
        Assert.Equal(1, projection.AggregateVersion);
    }

    [Fact]
    public async Task Consumer_IgnoresDuplicateAndStaleEventsWithoutRegressingProjection()
    {
        string queueName = $"accounts.customer-events.{Guid.NewGuid():N}";
        await using AccountsApiFactory accountsFactory = new(
            _fixture,
            enableMessaging: true,
            queueName);
        using HttpClient client = accountsFactory.CreateClient();
        using HttpResponseMessage liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        RabbitMqTestClient rabbitMq = new(_fixture);
        await rabbitMq.WaitForQueueAsync(queueName, EventuallyTimeout);
        Guid customerId = Guid.NewGuid();
        Guid currentEventId = Guid.NewGuid();
        string currentEvent = CreateCustomerEvent(
            currentEventId,
            customerId,
            aggregateVersion: 2,
            name: "Current Name");
        await rabbitMq.PublishAsync("customer.updated.v1", currentEvent);

        bool currentWasApplied = await WaitUntilAsync(async () =>
        {
            await using AsyncServiceScope scope = accountsFactory.Services.CreateAsyncScope();
            AccountsDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
            return await dbContext.CustomerProjections.AsNoTracking().AnyAsync(
                projection =>
                    projection.CustomerId == customerId &&
                    projection.AggregateVersion == 2);
        });
        Assert.True(currentWasApplied, "The current customer event was not applied in time.");

        await rabbitMq.PublishAsync("customer.updated.v1", currentEvent);
        string staleEvent = CreateCustomerEvent(
            Guid.NewGuid(),
            customerId,
            aggregateVersion: 1,
            name: "Stale Name");
        await rabbitMq.PublishAsync("customer.updated.v1", staleEvent);

        bool bothUniqueEventsWereProcessed = await WaitUntilAsync(async () =>
            await _fixture.CountInboxMessagesAsync() == 2);
        Assert.True(
            bothUniqueEventsWereProcessed,
            "The duplicate/stale event scenario was not processed in time.");

        await using AsyncServiceScope assertionScope =
            accountsFactory.Services.CreateAsyncScope();
        AccountsDbContext assertionDbContext =
            assertionScope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        CustomerProjection projection = await assertionDbContext.CustomerProjections
            .AsNoTracking()
            .SingleAsync(customer => customer.CustomerId == customerId);
        Assert.Equal("Current Name", projection.Name);
        Assert.Equal(2, projection.AggregateVersion);
    }

    private static string CreateCustomerEvent(
        Guid eventId,
        Guid customerId,
        long aggregateVersion,
        string name)
    {
        return JsonSerializer.Serialize(new
        {
            eventId,
            eventType = "customer.updated.v1",
            occurredAtUtc = DateTimeOffset.UtcNow,
            correlationId = Guid.NewGuid().ToString("N"),
            aggregateId = customerId,
            aggregateVersion,
            data = new
            {
                customerId,
                name,
                isActive = true,
                isDeleted = false,
            },
        });
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(EventuallyTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return false;
    }
}
