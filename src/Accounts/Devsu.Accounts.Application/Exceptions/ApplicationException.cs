namespace Devsu.Accounts.Application.Exceptions;

public abstract class ApplicationExceptionBase : Exception
{
    protected ApplicationExceptionBase(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class ValidationException : ApplicationExceptionBase
{
    public ValidationException(string code, string message)
        : base(code, message)
    {
    }
}

public sealed class NotFoundException : ApplicationExceptionBase
{
    public NotFoundException(string code, string message)
        : base(code, message)
    {
    }
}

public sealed class ConflictException : ApplicationExceptionBase
{
    public ConflictException(string code, string message, Exception? innerException = null)
        : base(code, message, innerException)
    {
    }
}
