namespace Devsu.Accounts.Domain.Exceptions;

public sealed class InsufficientFundsException : Exception
{
    public const string ErrorCode = "insufficient_funds";

    public InsufficientFundsException()
        : base("Saldo no disponible")
    {
        Code = ErrorCode;
    }

    public string Code { get; }
}
