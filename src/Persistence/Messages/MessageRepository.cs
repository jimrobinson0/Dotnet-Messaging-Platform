using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Core.Exceptions;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Messaging.Persistence.Messages.Mapping;
using Messaging.Persistence.Messages.Reads;
using Messaging.Persistence.Messages.Writes;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Npgsql;

namespace Messaging.Persistence.Messages;

public sealed class MessageRepository(
    DbConnectionFactory connectionFactory,
    MessageReader messageReader,
    MessageWriter messageWriter,
    ParticipantWriter participantWriter,
    ReviewWriter reviewWriter,
    AuditWriter auditWriter)
    : IMessageRepository
{
    private const string InvalidReplyTargetErrorCode = "INVALID_REPLY_TARGET";
    private const string ClaimNextApprovedSql = """
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
                                                      status = 'Sending'::core.message_status,
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
    public async Task<(Message Message, bool Inserted)> InsertAsync(
        Message message,
        bool requiresApprovalFromRequest,
        Func<Guid, MessageAuditEvent> auditEventFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(auditEventFactory);

        var record = InsertMessageRecordMapper.ToInsertRecord(message, requiresApprovalFromRequest);
        var participants = ParticipantPrototypeMapper.FromCore(message.Participants);

        (Guid MessageId, bool Inserted) insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory, cancellationToken: cancellationToken))
        {
            var resolvedCreateIntent =
                await ResolveReplyThreadingAsync(record, uow.Transaction, cancellationToken);

            insertResult = await messageWriter.InsertIdempotentAsync(
                resolvedCreateIntent,
                uow.Transaction,
                cancellationToken);
            if (insertResult.Inserted)
            {
                var persistedParticipants = ParticipantPrototypeMapper.Bind(insertResult.MessageId, participants);
                var persistedAuditEvent = auditEventFactory(insertResult.MessageId);
                ArgumentNullException.ThrowIfNull(persistedAuditEvent);

                await participantWriter.InsertAsync(persistedParticipants, uow.Transaction, cancellationToken);
                await auditWriter.InsertAsync(persistedAuditEvent, uow.Transaction, cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
        }

        var persisted = await GetByIdAsync(insertResult.MessageId, cancellationToken);
        return (persisted, insertResult.Inserted);
    }

    private async Task<InsertMessageRecord> ResolveReplyThreadingAsync(
        InsertMessageRecord record,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (record.ReplyToMessageId is null)
            return record with
            {
                ReplyToMessageId = null,
                InReplyTo = null,
                ReferencesHeader = null
            };

        var replyTarget =
            await messageReader.GetReplyTargetAsync(record.ReplyToMessageId.Value, transaction, cancellationToken);
        if (!IsValidReplyTarget(replyTarget))
            throw new MessageValidationException(
                InvalidReplyTargetErrorCode,
                "The reply target does not exist, is not sent, or lacks an SMTP message id.");

        var smtpMessageId = replyTarget!.SmtpMessageId!;
        var referencesHeader = string.IsNullOrWhiteSpace(replyTarget.ReferencesHeader)
            ? smtpMessageId
            : $"{replyTarget.ReferencesHeader} {smtpMessageId}";

        return record with
        {
            ReplyToMessageId = record.ReplyToMessageId,
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

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory, cancellationToken: cancellationToken))
        {
            var message = await messageReader.GetForUpdateAsync(messageId, uow.Transaction, cancellationToken);
            var reviewResult = applyDecision(message);

            await messageWriter.UpdateAsync(message, uow.Transaction, cancellationToken);
            await reviewWriter.InsertAsync(reviewResult.Review, uow.Transaction, cancellationToken);

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

            await auditWriter.InsertAsync(persistedAuditEvent, uow.Transaction, cancellationToken);
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

        await using var uow = await UnitOfWork.BeginAsync(connectionFactory, cancellationToken: cancellationToken);

        try
        {
            var command = new CommandDefinition(
                ClaimNextApprovedSql,
                new { WorkerId = workerId },
                uow.Transaction,
                cancellationToken: cancellationToken);

            var row = await uow.Connection.QuerySingleOrDefaultAsync<MessageRow>(command);

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
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await messageReader.GetByIdAsync(messageId, connection, cancellationToken);
    }

}
