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
public sealed class AccountMovementsApiIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public AccountMovementsApiIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetAccountsDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateMovement_PersistsMovementAndBalanceAtomically()
    {
        await using AccountsApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        Guid customerId = Guid.NewGuid();
        await ApiTestData.SeedCustomerProjectionAsync(factory, customerId);
        using HttpResponseMessage accountResponse = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100001",
            100m);
        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

        using HttpResponseMessage response = await ApiTestData.CreateMovementAsync(
            client,
            "100001",
            "Deposito",
            50m,
            "create-atomic-1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ApiTestData.ReadJsonAsync(response);
        Assert.Equal(150m, document.RootElement.GetProperty("saldoDisponible").GetDecimal());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AccountsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        Account account = await dbContext.Accounts.AsNoTracking().SingleAsync();
        AccountMovement movement = await dbContext.AccountMovements.AsNoTracking().SingleAsync();
        Assert.Equal(150m, account.CurrentBalance);
        Assert.Equal(150m, movement.Balance);
        Assert.Equal(50m, movement.Value);
    }

    [Fact]
    public async Task CreateWithdrawal_WithInsufficientFunds_ReturnsRequiredErrorAndChangesNothing()
    {
        await using AccountsApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        Guid customerId = Guid.NewGuid();
        await ApiTestData.SeedCustomerProjectionAsync(factory, customerId);
        using HttpResponseMessage accountResponse = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100002",
            25m);
        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

        using HttpResponseMessage response = await ApiTestData.CreateMovementAsync(
            client,
            "100002",
            "Retiro",
            -30m,
            "insufficient-1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using JsonDocument problem = await ApiTestData.ReadJsonAsync(response);
        Assert.Equal("Saldo no disponible", problem.RootElement.GetProperty("detail").GetString());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AccountsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        Assert.Equal(25m, (await dbContext.Accounts.AsNoTracking().SingleAsync()).CurrentBalance);
        Assert.Empty(await dbContext.AccountMovements.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateMovement_WithRepeatedIdempotencyKey_ReturnsOriginalWithoutDuplicate()
    {
        await using AccountsApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        Guid customerId = Guid.NewGuid();
        await ApiTestData.SeedCustomerProjectionAsync(factory, customerId);
        using HttpResponseMessage accountResponse = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100003",
            100m);
        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

        using HttpResponseMessage firstResponse = await ApiTestData.CreateMovementAsync(
            client,
            "100003",
            "Retiro",
            -20m,
            "idempotent-1");
        using HttpResponseMessage repeatedResponse = await ApiTestData.CreateMovementAsync(
            client,
            "100003",
            "Retiro",
            -20m,
            "idempotent-1");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        using JsonDocument first = await ApiTestData.ReadJsonAsync(firstResponse);
        using JsonDocument repeated = await ApiTestData.ReadJsonAsync(repeatedResponse);
        Assert.Equal(
            first.RootElement.GetProperty("movimientoId").GetInt64(),
            repeated.RootElement.GetProperty("movimientoId").GetInt64());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AccountsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        Assert.Equal(1, await dbContext.AccountMovements.CountAsync());
        Assert.Equal(80m, (await dbContext.Accounts.AsNoTracking().SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task ConcurrentWithdrawals_CannotSpendTheSameBalanceTwice()
    {
        await using AccountsApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        Guid customerId = Guid.NewGuid();
        await ApiTestData.SeedCustomerProjectionAsync(factory, customerId);
        using HttpResponseMessage accountResponse = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100004",
            100m);
        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

        Task<HttpResponseMessage> firstWithdrawal = ApiTestData.CreateMovementAsync(
            client,
            "100004",
            "Retiro",
            -80m,
            "concurrent-1");
        Task<HttpResponseMessage> secondWithdrawal = ApiTestData.CreateMovementAsync(
            client,
            "100004",
            "Retiro",
            -80m,
            "concurrent-2");
        HttpResponseMessage[] responses = await Task.WhenAll(firstWithdrawal, secondWithdrawal);
        using HttpResponseMessage firstResponse = responses[0];
        using HttpResponseMessage secondResponse = responses[1];

        HttpStatusCode[] statuses = responses
            .Select(response => response.StatusCode)
            .OrderBy(status => (int)status)
            .ToArray();
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity],
            statuses);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AccountsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        Assert.Equal(1, await dbContext.AccountMovements.CountAsync());
        Assert.Equal(20m, (await dbContext.Accounts.AsNoTracking().SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task CorrectMovement_RebuildsLaterBalancesAndCreatesSingleAuditEntry()
    {
        await using AccountsApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        Guid customerId = Guid.NewGuid();
        await ApiTestData.SeedCustomerProjectionAsync(factory, customerId);
        using HttpResponseMessage accountResponse = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100005",
            100m);
        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

        using HttpResponseMessage depositResponse = await ApiTestData.CreateMovementAsync(
            client,
            "100005",
            "Deposito",
            50m,
            "correction-deposit-1");
        using HttpResponseMessage withdrawalResponse = await ApiTestData.CreateMovementAsync(
            client,
            "100005",
            "Retiro",
            -30m,
            "correction-withdrawal-1");
        Assert.Equal(HttpStatusCode.Created, depositResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withdrawalResponse.StatusCode);

        using JsonDocument deposit = await ApiTestData.ReadJsonAsync(depositResponse);
        long depositId = deposit.RootElement.GetProperty("movimientoId").GetInt64();
        string occurredAtUtc = deposit.RootElement.GetProperty("fecha").GetString()!;
        object correctionPayload = new
        {
            fecha = occurredAtUtc,
            tipoMovimiento = "Deposito",
            valor = 100m,
            motivo = "Correct integration-test amount",
        };

        using HttpResponseMessage correctionResponse = await client.PutAsJsonAsync(
            $"/api/movimientos/{depositId}",
            correctionPayload);
        using HttpResponseMessage repeatedResponse = await client.PutAsJsonAsync(
            $"/api/movimientos/{depositId}",
            correctionPayload);

        Assert.Equal(HttpStatusCode.OK, correctionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AccountsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        AccountMovement[] movements = await dbContext.AccountMovements
            .AsNoTracking()
            .OrderBy(movement => movement.OccurredAtUtc)
            .ThenBy(movement => movement.Id)
            .ToArrayAsync();
        Assert.Equal([200m, 170m], movements.Select(movement => movement.Balance).ToArray());
        Assert.Equal(170m, (await dbContext.Accounts.AsNoTracking().SingleAsync()).CurrentBalance);
        Assert.Equal(1, await dbContext.AccountMovementCorrections.CountAsync());
    }

    [Fact]
    public async Task GetStatement_IncludesPeriodBoundariesAndAccountsWithoutMovements()
    {
        await using AccountsApiFactory factory = new(_fixture);
        using HttpClient client = factory.CreateClient();
        Guid customerId = Guid.NewGuid();
        await ApiTestData.SeedCustomerProjectionAsync(factory, customerId, "Statement Customer");
        using HttpResponseMessage firstAccount = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100006",
            100m);
        using HttpResponseMessage secondAccount = await ApiTestData.CreateAccountAsync(
            client,
            customerId,
            "100007",
            200m);
        Assert.Equal(HttpStatusCode.Created, firstAccount.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondAccount.StatusCode);

        using HttpResponseMessage movementResponse = await ApiTestData.CreateMovementAsync(
            client,
            "100006",
            "Deposito",
            50m,
            "statement-1");
        Assert.Equal(HttpStatusCode.Created, movementResponse.StatusCode);
        using JsonDocument movement = await ApiTestData.ReadJsonAsync(movementResponse);
        DateOnly movementDate = DateOnly.FromDateTime(
            movement.RootElement.GetProperty("fecha").GetDateTimeOffset().UtcDateTime);

        string requestUri =
            $"/api/reportes?fechaInicio={movementDate:yyyy-MM-dd}" +
            $"&fechaFin={movementDate:yyyy-MM-dd}&clienteId={customerId:D}";
        using HttpResponseMessage response = await client.GetAsync(requestUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument statement = await ApiTestData.ReadJsonAsync(response);
        JsonElement root = statement.RootElement;
        Assert.Equal("Statement Customer", root.GetProperty("cliente").GetProperty("nombre").GetString());
        JsonElement.ArrayEnumerator accounts = root.GetProperty("cuentas").EnumerateArray();
        JsonElement[] accountItems = accounts.OrderBy(
            item => item.GetProperty("numeroCuenta").GetString(),
            StringComparer.Ordinal).ToArray();
        Assert.Equal(2, accountItems.Length);
        Assert.Equal(100m, accountItems[0].GetProperty("saldoInicioPeriodo").GetDecimal());
        Assert.Equal(150m, accountItems[0].GetProperty("saldoFinPeriodo").GetDecimal());
        Assert.Single(accountItems[0].GetProperty("movimientos").EnumerateArray());
        Assert.Equal(200m, accountItems[1].GetProperty("saldoInicioPeriodo").GetDecimal());
        Assert.Equal(200m, accountItems[1].GetProperty("saldoFinPeriodo").GetDecimal());
        Assert.Empty(accountItems[1].GetProperty("movimientos").EnumerateArray());
    }
}
