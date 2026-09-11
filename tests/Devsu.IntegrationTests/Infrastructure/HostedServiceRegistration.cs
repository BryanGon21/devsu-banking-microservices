using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Devsu.IntegrationTests.Infrastructure;

internal static class HostedServiceRegistration
{
    public static void RemoveByImplementationName(
        IServiceCollection services,
        string implementationName)
    {
        ServiceDescriptor[] registrations = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                string.Equals(
                    descriptor.ImplementationType?.Name,
                    implementationName,
                    StringComparison.Ordinal))
            .ToArray();

        foreach (ServiceDescriptor registration in registrations)
        {
            services.Remove(registration);
        }
    }
}
