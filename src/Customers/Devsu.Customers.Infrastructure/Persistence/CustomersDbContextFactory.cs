using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devsu.Customers.Infrastructure.Persistence;

internal sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<CustomersDbContext> options = new();
        options.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=DevsuCustomersDb;Integrated Security=true;TrustServerCertificate=true");

        return new CustomersDbContext(options.Options);
    }
}
