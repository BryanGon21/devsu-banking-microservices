using Devsu.Accounts.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Devsu.IntegrationTests.Infrastructure;

internal sealed class AccountsApiFactory : WebApplicationFactory<AccountsApiAssemblyMarker>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly bool _enableMessaging;
    private readonly string _queueName;

    public AccountsApiFactory(
        IntegrationTestFixture fixture,
        bool enableMessaging = false,
        string? queueName = null)
    {
        _fixture = fixture;
        _enableMessaging = enableMessaging;
        _queueName = queueName ?? $"accounts.customer-events.{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:AccountsDatabase",
            _fixture.AccountsConnectionString);
        builder.UseSetting("RabbitMq:HostName", _fixture.RabbitMqHostName);
        builder.UseSetting(
            "RabbitMq:Port",
            _fixture.RabbitMqPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:UserName", _fixture.RabbitMqUser);
        builder.UseSetting("RabbitMq:Password", _fixture.RabbitMqSecret);
        builder.UseSetting("CustomerEvents:QueueName", _queueName);
        builder.UseSetting("CustomerEvents:DeadLetterQueue", $"{_queueName}.dead-letter");
        builder.UseSetting("CustomerEvents:ConnectionRetryDelay", "00:00:00.100");
        builder.UseSetting("CustomerEvents:BaseRetryDelay", "00:00:00.100");
        builder.UseSetting("CustomerEvents:MaximumRetryDelay", "00:00:01");

        if (!_enableMessaging)
        {
            builder.ConfigureServices(services =>
                HostedServiceRegistration.RemoveByImplementationName(
                    services,
                    "CustomerIntegrationEventConsumer"));
        }
    }
}
