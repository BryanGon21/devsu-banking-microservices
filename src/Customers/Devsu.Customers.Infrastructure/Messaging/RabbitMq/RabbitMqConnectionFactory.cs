using RabbitMQ.Client;

namespace Devsu.Customers.Infrastructure.Messaging.RabbitMq;

internal static class RabbitMqConnectionFactory
{
    public static ConnectionFactory Create(RabbitMqOptions options)
    {
        ConnectionFactory connectionFactory = new()
        {
            HostName = options.HostName,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            RequestedHeartbeat = options.RequestedHeartbeat,
            NetworkRecoveryInterval = options.NetworkRecoveryInterval,
        };

        if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            connectionFactory.UserName = options.UserName;
            connectionFactory.Password = options.Password!;
        }

        return connectionFactory;
    }
}
