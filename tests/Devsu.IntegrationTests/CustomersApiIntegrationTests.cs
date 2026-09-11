using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devsu.Customers.Domain.Entities;
using Devsu.Customers.Infrastructure.Persistence;
using Devsu.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Devsu.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class CustomersApiIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public CustomersApiIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetCustomersDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SwaggerDocument_UsesSpanishGenderContract()
    {
        await using CustomersApiFactory factory = new(
            _fixture,
            environment: "Development");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/swagger/v1/swagger.json");

        string responseContent = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Swagger document returned {(int)response.StatusCode}: {responseContent}");
        using JsonDocument document = JsonDocument.Parse(responseContent);
        JsonElement genderSchema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("CreateCustomerRequest")
            .GetProperty("properties")
            .GetProperty("genero");
        string[] values = genderSchema
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        string[] expectedValues = ["Masculino", "Femenino", "Otro"];

        Assert.Equal(expectedValues, values);
    }

    [Fact]
    public async Task CreateCustomer_PersistsHashedPasswordAndPendingOutboxWithoutLeakingSecrets()
    {
        await using CustomersApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        const string password = "1234";

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/clientes",
            CreateCustomerPayload("1717171717", password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string responseContent = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, responseContent, StringComparison.Ordinal);
        Assert.DoesNotContain("contrasena", responseContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", responseContent, StringComparison.OrdinalIgnoreCase);

        using JsonDocument document = JsonDocument.Parse(responseContent);
        JsonElement root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("clienteId").GetGuid());
        Assert.Equal("Integration Customer", root.GetProperty("nombre").GetString());
        Assert.Equal("Masculino", root.GetProperty("genero").GetString());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        CustomersDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Customer storedCustomer = await dbContext.Customers.AsNoTracking().SingleAsync();

        Assert.NotEqual(password, storedCustomer.PasswordHash);
        Assert.DoesNotContain(password, storedCustomer.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain(
            factory.LogSink.Messages,
            message => message.Contains(password, StringComparison.Ordinal));
        Assert.Equal(1, await _fixture.CountPendingOutboxMessagesAsync());
    }

    [Fact]
    public async Task CreateCustomer_WhenBrokerIsUnavailable_KeepsOutboxMessageForRetry()
    {
        await using CustomersApiFactory factory = new(
            _fixture,
            enableMessaging: true,
            rabbitMqPort: 1);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/clientes",
            CreateCustomerPayload("2020202020", "1234"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        bool publishWasAttempted = await WaitUntilAsync(async () =>
            await _fixture.GetMaximumOutboxAttemptCountAsync() > 0);
        Assert.True(publishWasAttempted, "The outbox publisher did not attempt delivery in time.");
        Assert.Equal(1, await _fixture.CountPendingOutboxMessagesAsync());
        Assert.Equal(0, await _fixture.CountPublishedOutboxMessagesAsync());
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateIdentification_ReturnsConflictAndRollsBack()
    {
        await using CustomersApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            "/api/clientes",
            CreateCustomerPayload("1818181818", "1234"));
        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/clientes",
            CreateCustomerPayload("1818181818", "5678"));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(
            "application/problem+json",
            duplicateResponse.Content.Headers.ContentType?.MediaType);

        using JsonDocument problem = await ApiTestData.ReadJsonAsync(duplicateResponse);
        Assert.Equal(
            "customer_duplicate_identification",
            problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(
            factory.LogSink.Messages,
            message => message.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        CustomersDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Assert.Equal(1, await dbContext.Customers.CountAsync());
        Assert.Equal(1, await _fixture.CountPendingOutboxMessagesAsync());
    }

    private static object CreateCustomerPayload(string identification, string password)
    {
        return new
        {
            nombre = "Integration Customer",
            genero = "Masculino",
            edad = 35,
            identificacion = identification,
            direccion = "Integration Address",
            telefono = "0999999999",
            contrasena = password,
            estado = true,
        };
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
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
