using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Messages;

internal static class ParticipantPrototypeMapper
{
    public static IReadOnlyCollection<MessageParticipantPrototype> FromCore(
        IEnumerable<MessageParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);

        return participants
            .Select(participant => new MessageParticipantPrototype(
                Id: participant.Id,
                Role: participant.Role,
                Address: participant.Address,
                DisplayName: participant.DisplayName,
                CreatedAt: participant.CreatedAt))
            .ToArray();
    }

    public static IReadOnlyCollection<MessageParticipant> Bind(
        Guid messageId,
        IEnumerable<MessageParticipantPrototype> participants)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("messageId must be a non-empty GUID.", nameof(messageId));
        }

        ArgumentNullException.ThrowIfNull(participants);

        return participants
            .Select(participant => new MessageParticipant(
                id: participant.Id,
                messageId: messageId,
                role: participant.Role,
                address: participant.Address,
                displayName: participant.DisplayName,
                createdAt: participant.CreatedAt))
            .ToArray();
    }
}
