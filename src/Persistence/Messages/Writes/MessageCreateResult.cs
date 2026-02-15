using Messaging.Core;

namespace Messaging.Persistence.Messages;

public sealed record MessageCreateResult(
    Message Message,
    bool WasCreated);