namespace Messaging.Application;

public sealed class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }
}
