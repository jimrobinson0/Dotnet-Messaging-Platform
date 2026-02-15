using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Core.Exceptions;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Npgsql;

namespace Messaging.Persistence.Messages;

public sealed class MessageRepository
{
    private const string InvalidReplyTargetErrorCode = "INVALID_REPLY_TARGET";
    internal const string ClaimNextApprovedSql = """
                                                     with cte as (
                                                         select id
                                                         from core.messages
                                                     where status = 'Approved'::core.message_status
                                                         order by created_at, id
                                                         for update skip locked
                                                         limit 1
                                                     ),
                                                 claimed as (
                                                     update core.messages m
                                                     set
                                                       -- attempt_count is incremented during actual delivery attempts by workers.
                                                       -- Claiming a message does not represent a delivery attempt.
                                                       status = 'Sending',
                                                       claimed_by = @WorkerId,
                                                       claimed_at = now(),
                                                       updated_at = now()
                                                     from cte
                                                     where m.id = cte.id
                                                    returning m.*
                                                 )
                                                 select
                                                   c.id as Id,
                                                   c.channel as Channel,
                                                   c.status as Status,
                                                   c.content_source::text as ContentSource,
                                                   c.created_at as CreatedAt,
                                                   c.updated_at as UpdatedAt,
                                                   c.claimed_by as ClaimedBy,
                                                   c.claimed_at as ClaimedAt,
                                                   c.sent_at as SentAt,
                                                   c.failure_reason as FailureReason,
                                                   c.attempt_count as AttemptCount,
                                                   c.template_key as TemplateKey,
                                                   c.template_version as TemplateVersion,
                                                   c.template_resolved_at as TemplateResolvedAt,
                                                   c.subject as Subject,
                                                   c.text_body as TextBody,
                                                   c.html_body as HtmlBody,
                                                   c.template_variables::text as TemplateVariablesJson,
                                                   c.idempotency_key as IdempotencyKey,
                                                   c.reply_to_message_id as ReplyToMessageId,
                                                   c.in_reply_to as InReplyTo,
                                                   c.references_header as ReferencesHeader,
                                                   c.smtp_message_id as SmtpMessageId
                                                 from claimed c;
                                                 """;

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

    public async Task<Message?> ClaimNextApprovedAsync(
        string workerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker id cannot be null or whitespace.", nameof(workerId));

        await using var uow = await UnitOfWork.BeginAsync(_connectionFactory, cancellationToken: cancellationToken);

        try
        {
            var row = await uow.Connection.QuerySingleOrDefaultAsync<MessageRow>(
                ClaimNextApprovedSql,
                new { WorkerId = workerId },
                uow.Transaction);

            if (row is null)
            {
                await uow.CommitAsync(cancellationToken);
                return null;
            }

            var message = MessageMapper.RehydrateMessage(row);
            await uow.CommitAsync(cancellationToken);
            return message;
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to claim next approved message.", ex);
        }
    }

    public async Task<Message> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await _messageReader.GetByIdAsync(messageId, connection, cancellationToken);
    }

}
