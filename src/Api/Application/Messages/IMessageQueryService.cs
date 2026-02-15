using Messaging.Api.Contracts.Messages;

namespace Messaging.Api.Application.Messages;

public interface IMessageQueryService
{
    Task<PagedResult<MessageSummary>> ListAsync(
        ListMessagesQuery query,
        CancellationToken cancellationToken = default);
}
