namespace Messaging.Application.Messages;

public interface IMessageQueryService
{
    Task<PagedResult<MessageSummary>> ListAsync(
        ListMessagesQuery query,
        CancellationToken cancellationToken = default);
}
