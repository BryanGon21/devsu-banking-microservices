using Devsu.Accounts.Domain.Entities;

namespace Devsu.Accounts.Application.Ports;

public interface ICustomerProjectionReader
{
    public Task<CustomerProjection?> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}
