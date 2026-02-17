using Messaging.Core.Exceptions;

namespace Messaging.Core;

public sealed class MessageParticipant
{
    public MessageParticipant(
        Guid id,
        Guid messageId,
        MessageParticipantRole role,
        string address,
        string? displayName,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new MessageValidationException(
                "PARTICIPANT_ADDRESS_REQUIRED",
                "Participant address is required.");

        Id = id;
        MessageId = messageId;
        Role = role;
        Address = address;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid MessageId { get; }
    public MessageParticipantRole Role { get; }
    public string Address { get; }
    public string? DisplayName { get; }
    public DateTimeOffset CreatedAt { get; }
}
