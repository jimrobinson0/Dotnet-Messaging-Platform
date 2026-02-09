namespace Messaging.Platform.Core;

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
        ArgumentNullException.ThrowIfNull(address);

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