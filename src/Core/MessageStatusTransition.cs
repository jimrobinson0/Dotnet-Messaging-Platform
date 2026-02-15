namespace Messaging.Core;

public readonly record struct MessageStatusTransition(
    Guid MessageId,
    MessageStatus FromStatus,
    MessageStatus ToStatus,
    DateTimeOffset OccurredAt);