using Devsu.Customers.Infrastructure.Messaging.Outbox;

namespace Devsu.Customers.UnitTests.Infrastructure;

public sealed class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    public void CalculateDelay_WithSuccessiveAttempts_AppliesExponentialBackoff(
        int attemptCount,
        int expectedSeconds)
    {
        TimeSpan delay = OutboxRetryPolicy.CalculateDelay(
            attemptCount,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void CalculateDelay_WhenExponentialDelayExceedsLimit_ReturnsMaximumDelay()
    {
        TimeSpan delay = OutboxRetryPolicy.CalculateDelay(
            30,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), delay);
    }
}
