using Messaging.Core.Audit;

namespace Messaging.Core;

public sealed class MessageAuditEvent
{
    public MessageAuditEvent(
        Guid id,
        Guid messageId,
        AuditEventType eventType,
        MessageStatus? fromStatus,
        MessageStatus? toStatus,
        string actorType,
        string actorId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(actorType);
        ArgumentNullException.ThrowIfNull(actorId);

        Id = id;
        MessageId = messageId;
        EventType = eventType;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorType = actorType;
        ActorId = actorId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; }
    public Guid MessageId { get; }
    public AuditEventType EventType { get; }
    public MessageStatus? FromStatus { get; }
    public MessageStatus? ToStatus { get; }
    public string ActorType { get; }
    public string ActorId { get; }
    public DateTimeOffset OccurredAt { get; }
}
