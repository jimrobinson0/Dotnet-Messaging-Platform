namespace Messaging.Platform.Persistence.Exceptions;

public sealed class ConcurrencyException : PersistenceException
{
    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
