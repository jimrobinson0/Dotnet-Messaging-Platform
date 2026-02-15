using Messaging.Core;

namespace Messaging.Api.Application.Messages;

public interface IMessageApplicationService
{
    Task<CreateMessageResult> CreateAsync(CreateMessageCommand command, CancellationToken cancellationToken = default);

    Task<Message> ApproveAsync(Guid messageId, ReviewMessageCommand command,
        CancellationToken cancellationToken = default);

    Task<Message> RejectAsync(Guid messageId, ReviewMessageCommand command,
        CancellationToken cancellationToken = default);

    Task<Message> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> ListAsync(string? status, int limit, DateTimeOffset? createdAfter,
        CancellationToken cancellationToken = default);
}