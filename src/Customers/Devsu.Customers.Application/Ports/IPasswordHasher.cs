namespace Devsu.Customers.Application.Ports;

public interface IPasswordHasher
{
    public string Hash(string password);
}
