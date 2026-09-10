using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Infrastructure.Health;
using Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;
using Devsu.Accounts.Infrastructure.Messaging.RabbitMq;
using Devsu.Accounts.Infrastructure.Persistence;
using Devsu.Accounts.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devsu.Accounts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("AccountsDatabase")
            ?? throw new InvalidOperationException(
                "The 'AccountsDatabase' connection string is not configured.");

        services.AddDbContext<AccountsDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountMovementReader, AccountMovementReader>();
        services.AddScoped<IAccountMovementWriter, SqlServerAccountMovementWriter>();
        services.AddScoped<IAccountMovementCorrector, SqlServerAccountMovementCorrector>();
        services.AddScoped<IAccountStatementReader, AccountStatementReader>();
        services.AddScoped<IAccountStatementReader, AccountStatementReader>();
        services.AddScoped<ICustomerProjectionReader, CustomerProjectionReader>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AccountsDbContext>());
        services.AddScoped<CustomerIntegrationEventHandler>();

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(RabbitMqOptions.IsValid, "The RabbitMQ configuration is invalid.")
            .ValidateOnStart();
        services.AddOptions<CustomerEventConsumerOptions>()
            .Bind(configuration.GetSection(CustomerEventConsumerOptions.SectionName))
            .Validate(
                CustomerEventConsumerOptions.IsValid,
                "The Customer event consumer configuration is invalid.")
            .ValidateOnStart();

        services.AddHostedService<CustomerIntegrationEventConsumer>();

        services.AddHealthChecks()
            .AddDbContextCheck<AccountsDbContext>("accounts-sqlserver", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("accounts-rabbitmq", tags: ["ready"]);

        return services;
    }
}
