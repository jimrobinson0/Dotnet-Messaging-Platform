using Messaging.Application.Messages;
using Messaging.Api.Contracts;

namespace Messaging.Api.Contracts.Messages;

public static class MessageSummaryMapping
{
    public static PagedResultResponse<MessageSummaryResponse> ToResponse(
        this PagedResult<MessageSummary> result)
    {
        return new PagedResultResponse<MessageSummaryResponse>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = result.Items.Select(item => new MessageSummaryResponse
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
}
