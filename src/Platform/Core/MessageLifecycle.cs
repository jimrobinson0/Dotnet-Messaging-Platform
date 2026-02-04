using Messaging.Platform.Core.Exceptions;

namespace Messaging.Platform.Core;

public static class MessageLifecycle
{
    public static bool IsValidTransition(MessageStatus from, MessageStatus to)
    {
        return from switch
        {
            MessageStatus.Draft => to is MessageStatus.Queued or MessageStatus.Canceled,
            MessageStatus.Queued => to is MessageStatus.PendingApproval or MessageStatus.Canceled,
            MessageStatus.PendingApproval => to is MessageStatus.Approved or MessageStatus.Rejected or MessageStatus.Canceled,
            MessageStatus.Approved => to is MessageStatus.Sending or MessageStatus.Canceled,
            MessageStatus.Sending => to is MessageStatus.Sent or MessageStatus.Failed,
            MessageStatus.Sent => false,
            MessageStatus.Failed => false,
            MessageStatus.Rejected => false,
            MessageStatus.Canceled => false,
            _ => false
        };
    }

    public static void EnsureValidTransition(MessageStatus from, MessageStatus to)
    {
        if (!IsValidTransition(from, to))
        {
            throw new InvalidMessageStatusTransitionException(from, to);
        }
    }

    public static bool IsContentMutable(MessageStatus status)
    {
        return status == MessageStatus.Draft;
    }

    public static bool IsSendable(MessageStatus status)
    {
        return status == MessageStatus.Approved;
    }

    public static bool IsTerminal(MessageStatus status)
    {
        return status is MessageStatus.Sent
            or MessageStatus.Failed
            or MessageStatus.Rejected
            or MessageStatus.Canceled;
    }
}
