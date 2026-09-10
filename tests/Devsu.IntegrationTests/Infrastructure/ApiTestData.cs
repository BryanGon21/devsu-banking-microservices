using System.Net.Http.Json;
using System.Text.Json;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Devsu.IntegrationTests.Infrastructure;

internal static class ApiTestData
{
    public static async Task SeedCustomerProjectionAsync(
        AccountsApiFactory factory,
        Guid customerId,
        string name = "Integration Customer")
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AccountsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        CustomerProjection projection = CustomerProjection.Create(
            customerId,
            name,
            isActive: true,
            isDeleted: false,
            aggregateVersion: 1,
            DateTimeOffset.UtcNow);
        await dbContext.CustomerProjections.AddAsync(projection);
        await dbContext.SaveChangesAsync();
    }

    public static Task<HttpResponseMessage> CreateAccountAsync(
        HttpClient client,
        Guid customerId,
        string accountNumber,
        decimal initialBalance)
    {
        return client.PostAsJsonAsync(
            "/api/cuentas",
            new
            {
                numeroCuenta = accountNumber,
                tipoCuenta = "Ahorros",
                saldoInicial = initialBalance,
                estado = true,
                clienteId = customerId,
            });
    }

    public static Task<HttpResponseMessage> CreateMovementAsync(
        HttpClient client,
        string accountNumber,
        string movementType,
        decimal value,
        string idempotencyKey)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/movimientos")
        {
            Content = JsonContent.Create(new
            {
                numeroCuenta = accountNumber,
                tipoMovimiento = movementType,
                valor = value,
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }
}
