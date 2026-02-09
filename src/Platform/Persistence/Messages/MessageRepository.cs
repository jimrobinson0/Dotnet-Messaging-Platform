using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Reviews;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageRepository
{
    private readonly AuditWriter _auditWriter;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly MessageReader _messageReader;
    private readonly MessageWriter _messageWriter;
    private readonly ParticipantWriter _participantWriter;
    private readonly ReviewWriter _reviewWriter;

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
        MessageCreateIntent createIntent,
        IReadOnlyCollection<MessageParticipantPrototype> participants,
        Func<Guid, MessageAuditEvent> auditEventFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createIntent);
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(auditEventFactory);

        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(_connectionFactory, cancellationToken: cancellationToken))
        {
            insertResult = await _messageWriter.InsertIdempotentAsync(createIntent, uow.Transaction, cancellationToken);
            if (insertResult.WasCreated)
            {
                var persistedParticipants = ParticipantPrototypeMapper.Bind(insertResult.MessageId, participants);
                var persistedAuditEvent = auditEventFactory(insertResult.MessageId);
                ArgumentNullException.ThrowIfNull(persistedAuditEvent);

                await _participantWriter.InsertAsync(persistedParticipants, uow.Transaction, cancellationToken);
                await _auditWriter.InsertAsync(persistedAuditEvent, uow.Transaction, cancellationToken);
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
                auditEvent.Id,
                messageId,
                auditEvent.EventType,
                reviewResult.Transition.FromStatus,
                reviewResult.Transition.ToStatus,
                auditEvent.ActorType,
                auditEvent.ActorId,
                reviewResult.Transition.OccurredAt,
                auditEvent.MetadataJson);

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