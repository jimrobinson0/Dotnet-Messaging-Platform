using Messaging.Core;

namespace Messaging.Persistence.Messages;

public sealed record MessageParticipantPrototype(
    Guid Id,
    MessageParticipantRole Role,
    string Address,
    string? DisplayName,
    DateTimeOffset CreatedAt);