namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal enum CustomerEventProcessingResult
{
    Applied = 1,
    IgnoredAsDuplicate = 2,
    IgnoredAsStale = 3,
}
