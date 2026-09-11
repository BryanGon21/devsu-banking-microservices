namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal sealed class CustomerEventConsumerOptions
{
    public const string SectionName = "CustomerEvents";

    public string QueueName { get; init; } = "accounts.customer-events.v1";

    public string DeadLetterExchange { get; init; } = "customer.events.dlx";

    public string DeadLetterQueue { get; init; } = "accounts.customer-events.v1.dead-letter";

    public ushort PrefetchCount { get; init; } = 20;

    public int MaximumRetries { get; init; } = 3;

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ConnectionRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    public int MaximumMessageBytes { get; init; } = 64 * 1024;

    public static bool IsValid(CustomerEventConsumerOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.QueueName) &&
            !string.IsNullOrWhiteSpace(options.DeadLetterExchange) &&
            !string.IsNullOrWhiteSpace(options.DeadLetterQueue) &&
            options.PrefetchCount > 0 &&
            options.MaximumRetries is >= 0 and <= 10 &&
            options.BaseRetryDelay > TimeSpan.Zero &&
            options.MaximumRetryDelay >= options.BaseRetryDelay &&
            options.ConnectionRetryDelay > TimeSpan.Zero &&
            options.MaximumMessageBytes is > 0 and <= 1_048_576;
    }
}
