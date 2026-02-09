using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Messages;

public sealed record MessageParticipantPrototype(
    Guid Id,
    MessageParticipantRole Role,
    string Address,
    string? DisplayName,
    DateTimeOffset CreatedAt);
