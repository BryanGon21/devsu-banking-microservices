using Devsu.Customers.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Devsu.IntegrationTests.Infrastructure;

internal sealed class CustomersApiFactory : WebApplicationFactory<CustomersApiAssemblyMarker>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly bool _enableMessaging;
    private readonly int? _rabbitMqPort;

    public CustomersApiFactory(
        IntegrationTestFixture fixture,
        bool enableMessaging = false,
        int? rabbitMqPort = null)
    {
        _fixture = fixture;
        _enableMessaging = enableMessaging;
        _rabbitMqPort = rabbitMqPort;
    }

    public TestLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:CustomersDatabase",
            _fixture.CustomersConnectionString);
        builder.UseSetting("RabbitMq:HostName", _fixture.RabbitMqHostName);
        builder.UseSetting(
            "RabbitMq:Port",
            (_rabbitMqPort ?? _fixture.RabbitMqPort).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:UserName", _fixture.RabbitMqUser);
        builder.UseSetting("RabbitMq:Password", _fixture.RabbitMqSecret);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Outbox:BaseRetryDelay", "00:00:00.100");
        builder.UseSetting("Outbox:MaximumRetryDelay", "00:00:01");
        builder.ConfigureLogging(logging => logging.AddProvider(LogSink));

        if (!_enableMessaging)
        {
            builder.ConfigureServices(services =>
                HostedServiceRegistration.RemoveByImplementationName(
                    services,
                    "OutboxPublisherWorker"));
        }
    }
}
