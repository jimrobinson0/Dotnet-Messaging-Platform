using Messaging.Core;

namespace Messaging.Application.Messages;

public sealed class MessageSummary
{
    public Guid Id { get; init; }
    public string Channel { get; init; } = null!;
    public MessageStatus Status { get; init; }
    public bool RequiresApproval { get; init; }
    public string? Subject { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public string? FailureReason { get; init; }
}
