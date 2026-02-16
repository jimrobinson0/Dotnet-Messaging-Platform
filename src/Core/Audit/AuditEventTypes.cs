namespace Messaging.Core.Audit;

public readonly record struct AuditEventType
{
    public string Value { get; }

    private AuditEventType(string value)
    {
        Value = value;
    }

    public static readonly AuditEventType MessageCreated = new("MessageCreated");
    public static readonly AuditEventType MessageApproved = new("MessageApproved");
    public static readonly AuditEventType MessageRejected = new("MessageRejected");
    public static readonly AuditEventType MessageClaimed = new("MessageClaimed");
    public static readonly AuditEventType MessageSent = new("MessageSent");
    public static readonly AuditEventType FailureRecorded = new("FailureRecorded");
    public static readonly AuditEventType MessageCanceled = new("MessageCanceled");

    public override string ToString() => Value;

    public static AuditEventType FromDatabase(string value)
    {
        return value switch
        {
            "MessageCreated" => MessageCreated,
            "MessageApproved" => MessageApproved,
            "MessageRejected" => MessageRejected,
            "MessageClaimed" => MessageClaimed,
            "MessageSent" => MessageSent,
            "FailureRecorded" => FailureRecorded,
            "MessageCanceled" => MessageCanceled,
            _ => throw new ArgumentException($"Unknown audit event type '{value}'.")
        };
    }
}
