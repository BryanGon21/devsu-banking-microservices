using Devsu.Customers.Application.Ports;
using Microsoft.AspNetCore.Identity;

namespace Devsu.Customers.Infrastructure.Security;

internal sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<string> _passwordHasher = new();

    public string Hash(string password)
    {
        return _passwordHasher.HashPassword("customer", password);
    }
}
