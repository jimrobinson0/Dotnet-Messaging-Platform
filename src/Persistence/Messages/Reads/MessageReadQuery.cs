using Messaging.Core;

namespace Messaging.Persistence.Messages.Reads;

public sealed record MessageReadQuery(
    int Page,
    int PageSize,
    IReadOnlyCollection<MessageStatus>? Status,
    string? Channel,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    DateTimeOffset? SentFrom,
    DateTimeOffset? SentTo,
    bool? RequiresApproval)
{
    public int Offset
    {
        get
        {
            if (Page < 1 || PageSize < 1)
                return 0;

            var offset = (Page - 1) * PageSize;
            return offset < 0 ? 0 : offset;
        }
    }
}
