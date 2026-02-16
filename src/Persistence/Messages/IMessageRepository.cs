using Messaging.Core;
using Messaging.Core.Audit;

namespace Messaging.Persistence.Messages;

public interface IMessageRepository
{
    Task<(Message Message, bool Inserted)> InsertAsync(
        Message message,
        bool requiresApprovalFromRequest,
        string actorType,
        string actorId,
        CancellationToken cancellationToken = default);

    Task<Message> ApplyReviewAsync(
        Guid messageId,
        Func<Message, ReviewDecisionResult> applyDecision,
        AuditEventType auditEventType,
        string actorType,
        string actorId,
        CancellationToken cancellationToken = default);

    Task<Message?> ClaimNextApprovedAsync(
        string workerId,
        CancellationToken cancellationToken = default);

    Task<Message> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);
}
