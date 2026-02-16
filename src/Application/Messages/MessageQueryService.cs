using Messaging.Persistence.Messages.Reads;

namespace Messaging.Application.Messages;

public sealed class MessageQueryService(IMessageReadRepository readRepository) : IMessageQueryService
{
    public async Task<PagedResult<MessageSummary>> ListAsync(
        ListMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);

        var readQuery = new MessageReadQuery(
            query.Page,
            query.PageSize,
            query.Status,
            query.Channel,
            query.CreatedFrom,
            query.CreatedTo,
            query.SentFrom,
            query.SentTo,
            query.RequiresApproval);

        var page = await readRepository.ListAsync(readQuery, cancellationToken);

        return new PagedResult<MessageSummary>
        {
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            Items = page.Items.Select(item => new MessageSummary
            {
                Id = item.Id,
                Channel = item.Channel,
                Status = item.Status,
                RequiresApproval = item.RequiresApproval,
                Subject = item.Subject,
                CreatedAt = item.CreatedAt,
                SentAt = item.SentAt,
                FailureReason = item.FailureReason
            }).ToArray()
        };
    }

    private static void Validate(ListMessagesQuery query)
    {
        if (query.Page < 1)
            throw new BadRequestException("Page must be >= 1.");

        if (query.PageSize < 1 || query.PageSize > 200)
            throw new BadRequestException("PageSize must be between 1 and 200.");

        if (query.CreatedFrom.HasValue && query.CreatedTo.HasValue && query.CreatedFrom > query.CreatedTo)
            throw new BadRequestException("CreatedFrom must be <= CreatedTo.");

        if (query.SentFrom.HasValue && query.SentTo.HasValue && query.SentFrom > query.SentTo)
            throw new BadRequestException("SentFrom must be <= SentTo.");

        var hasStatus = query.Status is { Count: > 0 };
        var hasCreatedFilter = query.CreatedFrom.HasValue || query.CreatedTo.HasValue;
        var hasSentFilter = query.SentFrom.HasValue || query.SentTo.HasValue;

        if (!hasStatus && !hasCreatedFilter && !hasSentFilter)
            throw new BadRequestException(
                "At least one of status, createdFrom/createdTo, or sentFrom/sentTo is required.");
    }
}
