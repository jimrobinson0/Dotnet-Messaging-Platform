namespace Messaging.Platform.Core;

public sealed class MessageAuditEvent
{
    public MessageAuditEvent(
        Guid id,
        Guid messageId,
        string eventType,
        MessageStatus? fromStatus,
        MessageStatus? toStatus,
        string actorType,
        string actorId,
        DateTimeOffset occurredAt,
        string? metadataJson)
    {
        ArgumentNullException.ThrowIfNull(eventType);
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
        MetadataJson = metadataJson;
    }

    public Guid Id { get; }
    public Guid MessageId { get; }
    public string EventType { get; }
    public MessageStatus? FromStatus { get; }
    public MessageStatus? ToStatus { get; }
    public string ActorType { get; }
    public string ActorId { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? MetadataJson { get; }
}
