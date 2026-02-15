namespace Messaging.Persistence.Exceptions;

public sealed class NotFoundException : PersistenceException
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}