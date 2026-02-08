using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Reviews;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly MessageReader _messageReader;
    private readonly MessageWriter _messageWriter;
    private readonly ParticipantWriter _participantWriter;
    private readonly ReviewWriter _reviewWriter;
    private readonly AuditWriter _auditWriter;

    public MessageRepository(
        DbConnectionFactory connectionFactory,
        MessageReader messageReader,
        MessageWriter messageWriter,
        ParticipantWriter participantWriter,
        ReviewWriter reviewWriter,
        AuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _messageReader = messageReader;
        _messageWriter = messageWriter;
        _participantWriter = participantWriter;
        _reviewWriter = reviewWriter;
        _auditWriter = auditWriter;
    }

    public async Task<MessageCreateResult> CreateAsync(
        Message message,
        IReadOnlyCollection<MessageParticipant> participants,
        MessageAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(auditEvent);

        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(_connectionFactory, cancellationToken: cancellationToken))
        {
            insertResult = await _messageWriter.InsertIdempotentAsync(message, uow.Transaction, cancellationToken);
            if (insertResult.WasCreated)
            {
                await _participantWriter.InsertAsync(participants, uow.Transaction, cancellationToken);
                await _auditWriter.InsertAsync(auditEvent, uow.Transaction, cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
        }

        var persisted = await GetByIdAsync(insertResult.MessageId, cancellationToken);
        return new MessageCreateResult(persisted, insertResult.WasCreated);
    }

    public async Task<Message> ApplyReviewAsync(
        Guid messageId,
        Func<Message, ReviewDecisionResult> applyDecision,
        MessageAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applyDecision);
        ArgumentNullException.ThrowIfNull(auditEvent);

        await using (var uow = await UnitOfWork.BeginAsync(_connectionFactory, cancellationToken: cancellationToken))
        {
            var message = await _messageReader.GetForUpdateAsync(messageId, uow.Transaction, cancellationToken);
            var reviewResult = applyDecision(message);

            await _messageWriter.UpdateAsync(message, uow.Transaction, cancellationToken);
            await _reviewWriter.InsertAsync(reviewResult.Review, uow.Transaction, cancellationToken);

            var persistedAuditEvent = new MessageAuditEvent(
                id: auditEvent.Id,
                messageId: messageId,
                eventType: auditEvent.EventType,
                fromStatus: reviewResult.Transition.FromStatus,
                toStatus: reviewResult.Transition.ToStatus,
                actorType: auditEvent.ActorType,
                actorId: auditEvent.ActorId,
                occurredAt: reviewResult.Transition.OccurredAt,
                metadataJson: auditEvent.MetadataJson);

            await _auditWriter.InsertAsync(persistedAuditEvent, uow.Transaction, cancellationToken);
            await uow.CommitAsync(cancellationToken);
        }

        return await GetByIdAsync(messageId, cancellationToken);
    }

    public async Task<Message> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await _messageReader.GetByIdAsync(messageId, connection, cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListAsync(
        MessageStatus? status,
        int limit,
        DateTimeOffset? createdAfter,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await _messageReader.ListAsync(status, limit, createdAfter, connection, cancellationToken);
    }
}
