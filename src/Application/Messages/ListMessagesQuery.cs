using Messaging.Core;

namespace Messaging.Application.Messages;

public sealed class ListMessagesQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public IReadOnlyList<MessageStatus>? Status { get; init; }
    public string? Channel { get; init; }
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public DateTimeOffset? SentFrom { get; init; }
    public DateTimeOffset? SentTo { get; init; }
    public bool? RequiresApproval { get; init; }
}
