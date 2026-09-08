using Devsu.Customers.Application.Ports;
using Devsu.Customers.Infrastructure.Health;
using Devsu.Customers.Infrastructure.Messaging.Outbox;
using Devsu.Customers.Infrastructure.Messaging.RabbitMq;
using Devsu.Customers.Infrastructure.Persistence;
using Devsu.Customers.Infrastructure.Repositories;
using Devsu.Customers.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devsu.Customers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("CustomersDatabase")
            ?? throw new InvalidOperationException(
                "The 'CustomersDatabase' connection string is not configured.");

        services.AddDbContext<CustomersDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CustomersDbContext>());
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();

        services.AddOptions<OutboxPublisherOptions>()
            .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
            .Validate(OutboxPublisherOptions.IsValid, "The outbox publisher configuration is invalid.")
            .ValidateOnStart();

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(RabbitMqOptions.IsValid, "The RabbitMQ configuration is invalid.")
            .ValidateOnStart();

        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<OutboxProcessor>();
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
        services.AddHostedService<OutboxPublisherWorker>();

        services.AddHealthChecks()
            .AddDbContextCheck<CustomersDbContext>("customers-sqlserver", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("customers-rabbitmq", tags: ["ready"]);

        return services;
    }
}
