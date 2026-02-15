using Messaging.Persistence.Messages.Reads;

namespace Messaging.Persistence.Messages;

public interface IMessageReadRepository
{
    Task<PagedReadResult<MessageReadItem>> ListAsync(
        MessageReadQuery query,
        CancellationToken cancellationToken = default);
}
