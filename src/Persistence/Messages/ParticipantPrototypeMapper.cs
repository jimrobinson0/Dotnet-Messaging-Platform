using Messaging.Core;

namespace Messaging.Persistence.Messages;

internal static class ParticipantPrototypeMapper
{
    public static IReadOnlyCollection<MessageParticipantPrototype> FromCore(
        IEnumerable<MessageParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);

        return participants
            .Select(participant => new MessageParticipantPrototype(
                participant.Id,
                participant.Role,
                participant.Address,
                participant.DisplayName,
                participant.CreatedAt))
            .ToArray();
    }

    public static IReadOnlyCollection<MessageParticipant> Bind(
        Guid messageId,
        IEnumerable<MessageParticipantPrototype> participants)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("messageId must be a non-empty GUID.", nameof(messageId));

        ArgumentNullException.ThrowIfNull(participants);

        return participants
            .Select(participant => new MessageParticipant(
                participant.Id,
                messageId,
                participant.Role,
                participant.Address,
                participant.DisplayName,
                participant.CreatedAt))
            .ToArray();
    }
}