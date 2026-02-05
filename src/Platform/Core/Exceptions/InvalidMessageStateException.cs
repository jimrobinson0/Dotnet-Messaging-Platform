using Messaging.Platform.Core;

namespace Messaging.Platform.Core.Exceptions;

public sealed class InvalidMessageStateException : Exception
{
    public InvalidMessageStateException(MessageStatus status)
        : base($"Message status '{status}' is invalid or no longer supported.")
    {
        Status = status;
    }

    public InvalidMessageStateException(string status)
        : base($"Message status '{status}' is invalid or no longer supported.")
    {
        RawStatus = status;
    }

    public MessageStatus? Status { get; }
    public string? RawStatus { get; }
}
