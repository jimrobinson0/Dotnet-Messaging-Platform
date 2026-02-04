namespace Messaging.Platform.Core;

public sealed class MessageReview
{
    public MessageReview(
        Guid id,
        Guid messageId,
        ReviewDecision decision,
        string decidedBy,
        DateTimeOffset decidedAt,
        string? notes)
    {
        ArgumentNullException.ThrowIfNull(decidedBy);

        Id = id;
        MessageId = messageId;
        Decision = decision;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;
        Notes = notes;
    }

    public Guid Id { get; }
    public Guid MessageId { get; }
    public ReviewDecision Decision { get; }
    public string DecidedBy { get; }
    public DateTimeOffset DecidedAt { get; }
    public string? Notes { get; }
}
