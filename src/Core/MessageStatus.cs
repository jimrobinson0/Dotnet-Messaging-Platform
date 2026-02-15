namespace Messaging.Core;

public enum MessageStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Sending,
    Sent,
    Failed,
    Canceled
}