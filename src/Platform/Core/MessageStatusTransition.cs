namespace Messaging.Platform.Core;

public readonly record struct MessageStatusTransition(
    Guid MessageId,
    MessageStatus FromStatus,
    MessageStatus ToStatus,
    DateTimeOffset OccurredAt);