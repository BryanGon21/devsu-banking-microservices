namespace Devsu.Customers.Infrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public string ConnectionName { get; init; } = "customers-service";

    public TimeSpan RequestedHeartbeat { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan NetworkRecoveryInterval { get; init; } = TimeSpan.FromSeconds(5);

    public static bool IsValid(RabbitMqOptions options)
    {
        bool credentialsAreComplete =
            string.IsNullOrWhiteSpace(options.UserName) == string.IsNullOrWhiteSpace(options.Password);

        return !string.IsNullOrWhiteSpace(options.HostName) &&
            options.Port is > 0 and <= 65_535 &&
            !string.IsNullOrWhiteSpace(options.VirtualHost) &&
            !string.IsNullOrWhiteSpace(options.ConnectionName) &&
            options.RequestedHeartbeat > TimeSpan.Zero &&
            options.NetworkRecoveryInterval > TimeSpan.Zero &&
            credentialsAreComplete;
    }
}
