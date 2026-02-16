using Messaging.Core;

namespace Messaging.Persistence.Messages;

internal sealed record MessageParticipantPrototype(
    Guid Id,
    MessageParticipantRole Role,
    string Address,
    string? DisplayName,
    DateTimeOffset CreatedAt);
