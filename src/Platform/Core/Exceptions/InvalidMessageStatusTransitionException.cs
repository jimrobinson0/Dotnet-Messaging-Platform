namespace Messaging.Platform.Core.Exceptions;

public sealed class InvalidMessageStatusTransitionException : Exception
{
    public InvalidMessageStatusTransitionException(MessageStatus fromStatus, MessageStatus toStatus)
        : base($"Invalid message status transition from '{fromStatus}' to '{toStatus}'.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public MessageStatus FromStatus { get; }
    public MessageStatus ToStatus { get; }
}