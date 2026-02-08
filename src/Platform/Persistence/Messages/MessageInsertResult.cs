namespace Messaging.Platform.Persistence.Messages;

public readonly record struct MessageInsertResult(
    Guid MessageId,
    bool WasCreated);
