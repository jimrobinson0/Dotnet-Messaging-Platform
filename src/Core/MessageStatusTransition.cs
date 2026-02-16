namespace Messaging.Core;

public readonly record struct MessageStatusTransition(
    MessageStatus FromStatus,
    MessageStatus ToStatus);
