using Devsu.Accounts.Infrastructure.Persistence;
using Devsu.Customers.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Devsu.IntegrationTests.Infrastructure;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private const string CustomersDatabaseName = "CustomersIntegrationDb";
    private const string AccountsDatabaseName = "AccountsIntegrationDb";
    private const string RabbitMqUserName = "integration";
    private const string RabbitMqPassword = "Integration_Only_42!";
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
    private const string RabbitMqImage = "rabbitmq:3.13.7-alpine";

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(SqlServerImage).Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(RabbitMqImage)
        .WithUsername(RabbitMqUserName)
        .WithPassword(RabbitMqPassword)
        .Build();

    public string CustomersConnectionString { get; private set; } = string.Empty;

    public string AccountsConnectionString { get; private set; } = string.Empty;

    public string RabbitMqHostName => _rabbitMq.Hostname;

    public int RabbitMqPort => _rabbitMq.GetMappedPublicPort(5672);

    public string RabbitMqUser => RabbitMqUserName;

    public string RabbitMqSecret => RabbitMqPassword;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sqlServer.StartAsync(),
            _rabbitMq.StartAsync());

        CustomersConnectionString = BuildDatabaseConnectionString(CustomersDatabaseName);
        AccountsConnectionString = BuildDatabaseConnectionString(AccountsDatabaseName);

        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMq.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    public async Task ResetCustomersDatabaseAsync()
    {
        const string commandText = """
            DELETE FROM [customers].[OutboxMessages];
            DELETE FROM [customers].[Customers];
            DELETE FROM [customers].[People];
            """;

        await ExecuteSqlAsync(CustomersConnectionString, commandText);
    }

    public async Task ResetAccountsDatabaseAsync()
    {
        const string commandText = """
            DELETE FROM [accounts].[AccountMovementCorrections];
            DELETE FROM [accounts].[AccountMovements];
            DELETE FROM [accounts].[Accounts];
            DELETE FROM [accounts].[InboxMessages];
            DELETE FROM [accounts].[CustomerProjections];
            DBCC CHECKIDENT ('[accounts].[AccountMovementCorrections]', RESEED, 0);
            DBCC CHECKIDENT ('[accounts].[AccountMovements]', RESEED, 0);
            """;

        await ExecuteSqlAsync(AccountsConnectionString, commandText);
    }

    public Task<int> CountPendingOutboxMessagesAsync()
    {
        const string commandText = """
            SELECT COUNT(*)
            FROM [customers].[OutboxMessages]
            WHERE [PublishedAtUtc] IS NULL;
            """;

        return ExecuteScalarInt32Async(CustomersConnectionString, commandText);
    }

    public Task<int> CountPublishedOutboxMessagesAsync()
    {
        const string commandText = """
            SELECT COUNT(*)
            FROM [customers].[OutboxMessages]
            WHERE [PublishedAtUtc] IS NOT NULL;
            """;

        return ExecuteScalarInt32Async(CustomersConnectionString, commandText);
    }

    public Task<int> GetMaximumOutboxAttemptCountAsync()
    {
        const string commandText = """
            SELECT COALESCE(MAX([AttemptCount]), 0)
            FROM [customers].[OutboxMessages];
            """;

        return ExecuteScalarInt32Async(CustomersConnectionString, commandText);
    }

    public Task<int> CountInboxMessagesAsync()
    {
        const string commandText = "SELECT COUNT(*) FROM [accounts].[InboxMessages];";
        return ExecuteScalarInt32Async(AccountsConnectionString, commandText);
    }

    private string BuildDatabaseConnectionString(string databaseName)
    {
        SqlConnectionStringBuilder builder = new(_sqlServer.GetConnectionString())
        {
            InitialCatalog = databaseName,
        };

        return builder.ConnectionString;
    }

    private async Task ApplyMigrationsAsync()
    {
        DbContextOptions<CustomersDbContext> customersOptions =
            new DbContextOptionsBuilder<CustomersDbContext>()
                .UseSqlServer(CustomersConnectionString)
                .Options;
        await using (CustomersDbContext customersDbContext = new(customersOptions))
        {
            await customersDbContext.Database.MigrateAsync();
        }

        DbContextOptions<AccountsDbContext> accountsOptions =
            new DbContextOptionsBuilder<AccountsDbContext>()
                .UseSqlServer(AccountsConnectionString)
                .Options;
        await using AccountsDbContext accountsDbContext = new(accountsOptions);
        await accountsDbContext.Database.MigrateAsync();
    }

    private static async Task ExecuteSqlAsync(string connectionString, string commandText)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ExecuteScalarInt32Async(
        string connectionString,
        string commandText)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
