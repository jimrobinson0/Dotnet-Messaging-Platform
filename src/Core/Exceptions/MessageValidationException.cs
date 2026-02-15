namespace Messaging.Core.Exceptions;

public sealed class MessageValidationException : Exception
{
    public string Code { get; }

    public MessageValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public MessageValidationException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }
}
