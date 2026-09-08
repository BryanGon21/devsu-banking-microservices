using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal sealed class OutboxProcessor
{
    private readonly IOutboxStore _outboxStore;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly OutboxPublisherOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IOutboxStore outboxStore,
        IIntegrationEventPublisher publisher,
        IOptions<OutboxPublisherOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxProcessor> logger)
    {
        _outboxStore = outboxStore;
        _publisher = publisher;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        Guid leaseId = Guid.NewGuid();
        DateTimeOffset nowUtc = _timeProvider.GetUtcNow();
        IReadOnlyCollection<OutboxMessage> messages = await _outboxStore.ClaimPendingAsync(
            leaseId,
            nowUtc,
            _options.LeaseDuration,
            _options.BatchSize,
            cancellationToken);

        foreach (OutboxMessage message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);

                bool markedAsPublished = await _outboxStore.MarkPublishedAsync(
                    message.EventId,
                    leaseId,
                    _timeProvider.GetUtcNow(),
                    cancellationToken);

                if (!markedAsPublished)
                {
                    _logger.LogWarning(
                        "Outbox lease {LeaseId} was lost after publishing event {EventId}.",
                        leaseId,
                        message.EventId);
                }
                else
                {
                    _logger.LogInformation(
                        "Published integration event {EventId} of type {EventType} on attempt {AttemptCount}.",
                        message.EventId,
                        message.EventType,
                        message.AttemptCount);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                TimeSpan retryDelay = OutboxRetryPolicy.CalculateDelay(
                    Math.Max(message.AttemptCount, 1),
                    _options.BaseRetryDelay,
                    _options.MaximumRetryDelay);
                DateTimeOffset nextAttemptAtUtc = _timeProvider.GetUtcNow().Add(retryDelay);
                string error = GetSafeError(exception);

                bool markedAsFailed = await _outboxStore.MarkFailedAsync(
                    message.EventId,
                    leaseId,
                    nextAttemptAtUtc,
                    error,
                    cancellationToken);

                _logger.LogWarning(
                    exception,
                    "Could not publish event {EventId} of type {EventType}. " +
                    "Retry is scheduled for {NextAttemptAtUtc}; outbox state updated: {StateUpdated}.",
                    message.EventId,
                    message.EventType,
                    nextAttemptAtUtc,
                    markedAsFailed);
            }
        }

        return messages.Count;
    }

    private static string GetSafeError(Exception exception)
    {
        Exception rootException = exception.GetBaseException();
        string error = $"{rootException.GetType().Name}: {rootException.Message}"
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        return error.Length <= OutboxMessage.MaximumErrorLength
            ? error
            : error[..OutboxMessage.MaximumErrorLength];
    }
}
