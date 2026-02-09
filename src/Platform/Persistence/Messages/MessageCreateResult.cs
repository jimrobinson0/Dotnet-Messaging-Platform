using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Messages;

public sealed record MessageCreateResult(
    Message Message,
    bool WasCreated);