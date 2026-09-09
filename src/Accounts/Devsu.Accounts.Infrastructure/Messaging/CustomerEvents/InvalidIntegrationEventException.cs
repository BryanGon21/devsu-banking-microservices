namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal sealed class InvalidIntegrationEventException : Exception
{
    public InvalidIntegrationEventException(string message)
        : base(message)
    {
    }

    public InvalidIntegrationEventException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
