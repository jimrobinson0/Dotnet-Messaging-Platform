namespace Messaging.Platform.Core;

public enum MessageStatus
{
    Draft,
    Queued,
    PendingApproval,
    Approved,
    Rejected,
    Sending,
    Sent,
    Failed,
    Canceled
}
