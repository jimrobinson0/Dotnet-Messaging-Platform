using System.Data.Common;
using Messaging.Platform.Core;
using Messaging.Platform.Core.Exceptions;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Reviews;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageRepository
{
    private const string InvalidReplyTargetErrorCode = "INVALID_REPLY_TARGET";

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
            var resolvedCreateIntent =
                await ResolveReplyThreadingAsync(createIntent, uow.Transaction, cancellationToken);

            insertResult = await _messageWriter.InsertIdempotentAsync(
                resolvedCreateIntent,
                uow.Transaction,
                cancellationToken);
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

    private async Task<MessageCreateIntent> ResolveReplyThreadingAsync(
        MessageCreateIntent createIntent,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (createIntent.ReplyToMessageId is null)
            return createIntent with
            {
                ReplyToMessageId = null,
                InReplyTo = null,
                ReferencesHeader = null
            };

        var replyTarget =
            await _messageReader.GetReplyTargetAsync(createIntent.ReplyToMessageId.Value, transaction, cancellationToken);
        if (!IsValidReplyTarget(replyTarget))
            throw new MessageValidationException(
                InvalidReplyTargetErrorCode,
                "The reply target does not exist, is not sent, or lacks an SMTP message id.");

        var smtpMessageId = replyTarget!.SmtpMessageId!;
        var referencesHeader = string.IsNullOrWhiteSpace(replyTarget.ReferencesHeader)
            ? smtpMessageId
            : $"{replyTarget.ReferencesHeader} {smtpMessageId}";

        return createIntent with
        {
            ReplyToMessageId = createIntent.ReplyToMessageId,
            InReplyTo = smtpMessageId,
            ReferencesHeader = referencesHeader
        };
    }

    private static bool IsValidReplyTarget(ReplyTargetRow? replyTarget)
    {
        if (replyTarget is null) return false;

        var isSent = replyTarget.Status == MessageStatus.Sent;
        var hasSmtpMessageId = !string.IsNullOrWhiteSpace(replyTarget.SmtpMessageId);

        return isSent && hasSmtpMessageId;
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
