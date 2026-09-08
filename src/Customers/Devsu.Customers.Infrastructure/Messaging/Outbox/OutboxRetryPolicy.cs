namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal static class OutboxRetryPolicy
{
    public static TimeSpan CalculateDelay(
        int attemptCount,
        TimeSpan baseDelay,
        TimeSpan maximumDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptCount, 1);

        int exponent = Math.Min(attemptCount - 1, 30);
        double delayMilliseconds = baseDelay.TotalMilliseconds * Math.Pow(2, exponent);

        return TimeSpan.FromMilliseconds(Math.Min(delayMilliseconds, maximumDelay.TotalMilliseconds));
    }
}
