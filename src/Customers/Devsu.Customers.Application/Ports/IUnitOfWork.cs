namespace Devsu.Customers.Application.Ports;

public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
