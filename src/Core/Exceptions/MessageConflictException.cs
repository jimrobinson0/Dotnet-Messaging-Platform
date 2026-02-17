namespace Messaging.Core.Exceptions;

public sealed class MessageConflictException : Exception
{
    public string Code { get; }

    public MessageConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public MessageConflictException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }
}
