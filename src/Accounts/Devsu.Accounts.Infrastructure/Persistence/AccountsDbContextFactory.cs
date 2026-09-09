using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devsu.Accounts.Infrastructure.Persistence;

internal sealed class AccountsDbContextFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
    public AccountsDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<AccountsDbContext> options = new();
        options.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=DevsuAccountsDb;Integrated Security=true;TrustServerCertificate=true");

        return new AccountsDbContext(options.Options);
    }
}
