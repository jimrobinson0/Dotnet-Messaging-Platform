namespace Messaging.Platform.Persistence.Audit;

internal static class AuditEventTypes
{
    public const string MessageCreated = "MessageCreated";
    public const string MessageApproved = "MessageApproved";
    public const string MessageRejected = "MessageRejected";
    public const string MessageCanceled = "MessageCanceled";
}
