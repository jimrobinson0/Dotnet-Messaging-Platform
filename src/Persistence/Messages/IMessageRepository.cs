using Messaging.Core;

namespace Messaging.Persistence.Messages;

public interface IMessageRepository
{
    Task<(Message Message, bool Inserted)> InsertAsync(
        Message message,
        bool requiresApprovalFromRequest,
        Func<Guid, MessageAuditEvent> auditEventFactory,
        CancellationToken cancellationToken = default);

    Task<Message> ApplyReviewAsync(
        Guid messageId,
        Func<Message, ReviewDecisionResult> applyDecision,
        MessageAuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    Task<Message?> ClaimNextApprovedAsync(
        string workerId,
        CancellationToken cancellationToken = default);

    Task<Message> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);
}
