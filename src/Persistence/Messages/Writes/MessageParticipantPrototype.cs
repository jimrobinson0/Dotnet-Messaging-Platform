using Messaging.Core;

namespace Messaging.Persistence.Messages.Writes;

internal sealed record MessageParticipantPrototype(
    Guid Id,
    MessageParticipantRole Role,
    string Address,
    string? DisplayName,
    DateTimeOffset CreatedAt);
