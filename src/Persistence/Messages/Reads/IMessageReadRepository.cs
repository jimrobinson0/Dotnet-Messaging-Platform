namespace Messaging.Persistence.Messages.Reads;

public interface IMessageReadRepository
{
    Task<PagedReadResult<MessageReadItem>> ListAsync(
        MessageReadQuery query,
        CancellationToken cancellationToken = default);
}
