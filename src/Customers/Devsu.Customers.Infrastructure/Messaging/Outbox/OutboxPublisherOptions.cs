namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal sealed class OutboxPublisherOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; init; } = 20;

    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(5);

    public static bool IsValid(OutboxPublisherOptions options)
    {
        return options.BatchSize is > 0 and <= 100 &&
            options.PollingInterval > TimeSpan.Zero &&
            options.LeaseDuration > options.PollingInterval &&
            options.BaseRetryDelay > TimeSpan.Zero &&
            options.MaximumRetryDelay >= options.BaseRetryDelay;
    }
}
