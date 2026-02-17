using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Messaging.Persistence.Messages.Mapping;
using Npgsql;

namespace Messaging.Persistence.Messages.Writes;

public sealed class MessageWriter
{
    // Idempotent insert contract:
    // - Always returns a row.
    // - Id is newly inserted id or existing id for idempotency replay.
    // - Inserted is true when a new row was inserted.
    internal const string InsertIdempotentSql = """
                                                WITH inserted AS (
                                                    INSERT INTO core.messages (
                                                        idempotency_key,
                                                        channel,
                                                        status,
                                                        content_source,
                                                        requires_approval,
                                                        template_key,
                                                        template_version,
                                                        template_resolved_at,
                                                        subject,
                                                        text_body,
                                                        html_body,
                                                        template_variables,
                                                        reply_to_message_id,
                                                        in_reply_to,
                                                        references_header
                                                    )
                                                    VALUES (
                                                        @IdempotencyKey,
                                                        @Channel,
                                                        @Status::core.message_status,
                                                        @ContentSource::core.message_content_source,
                                                        @RequiresApproval,
                                                        @TemplateKey,
                                                        @TemplateVersion,
                                                        @TemplateResolvedAt,
                                                        @Subject,
                                                        @TextBody,
                                                        @HtmlBody,
                                                        @TemplateVariables::jsonb,
                                                        @ReplyToMessageId,
                                                        @InReplyTo,
                                                        @ReferencesHeader
                                                    )
                                                    ON CONFLICT (idempotency_key)
                                                    DO NOTHING
                                                    RETURNING id
                                                )
                                                SELECT id, true AS inserted FROM inserted
                                                UNION ALL
                                                SELECT m.id, false AS inserted
                                                FROM core.messages m
                                                WHERE m.idempotency_key = @IdempotencyKey
                                                  AND NOT EXISTS (SELECT 1 FROM inserted);
                                                """;

    // Fallback for PostgreSQL MVCC race under READ COMMITTED:
    // When concurrent transactions race on INSERT ... ON CONFLICT DO NOTHING,
    // the losing transaction's statement snapshot may predate the winner's COMMIT.
    // The CTE SELECT sees the stale snapshot and returns 0 rows.
    // A new statement gets a fresh snapshot and resolves the existing row.
    private const string ConcurrentReplaySql = """
                                               SELECT m.id, false AS inserted
                                               FROM core.messages m
                                               WHERE m.idempotency_key = @IdempotencyKey;
                                               """;

    private const string UpdateSql = """
                                     update core.messages
                                     set
                                       status = @Status::core.message_status,
                                       updated_at = now(),
                                       claimed_by = @ClaimedBy,
                                       claimed_at = CASE WHEN @ClaimedBy IS NOT NULL AND core.messages.claimed_at IS NULL THEN now() ELSE core.messages.claimed_at END,
                                       sent_at = CASE WHEN @Status::text = 'Sent' THEN now() ELSE core.messages.sent_at END,
                                       failure_reason = @FailureReason,
                                       attempt_count = @AttemptCount
                                     where id = @Id;
                                     """;

    internal async Task<(Guid MessageId, bool Inserted)> InsertIdempotentAsync(
        InsertMessageRecord record,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var connection = DbGuard.GetConnection(transaction);
        var parameters = BuildInsertParameters(record);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<MessageInsertRow>(
                new CommandDefinition(
                    InsertIdempotentSql,
                    parameters,
                    transaction,
                    cancellationToken: cancellationToken));

            // Under concurrent transactions, PostgreSQL READ COMMITTED MVCC snapshot
            // may not see a row committed after the statement snapshot was taken.
            // The INSERT detects the conflict at the index level (DO NOTHING),
            // but the UNION ALL SELECT uses the stale snapshot and returns 0 rows.
            // A fresh statement resolves the committed row.
            if (row is null)
            {
                row = await connection.QuerySingleAsync<MessageInsertRow>(
                    new CommandDefinition(
                        ConcurrentReplaySql,
                        new { record.IdempotencyKey },
                        transaction,
                        cancellationToken: cancellationToken));
            }

            if (row.Id == Guid.Empty)
                throw new PersistenceException(
                    $"InsertIdempotentSql returned empty id for idempotencyKey={record.IdempotencyKey}");

            return (row.Id, row.Inserted);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to insert message idempotently.", ex);
        }
    }

    internal async Task UpdateAsync(Message message, DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var connection = DbGuard.GetConnection(transaction);

        var parameters = new
        {
            message.Id,
            Status = message.Status.ToString(),
            message.ClaimedBy,
            message.FailureReason,
            message.AttemptCount
        };

        try
        {
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(UpdateSql, parameters, transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new NotFoundException($"Message '{message.Id}' was not found.");
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to update message.", ex);
        }
    }

    private static object BuildInsertParameters(InsertMessageRecord record)
    {
        return new
        {
            record.Channel,
            Status = record.Status.ToString(),
            record.RequiresApproval,
            ContentSource = record.ContentSource.ToString(),
            record.TemplateKey,
            record.TemplateVersion,
            record.TemplateResolvedAt,
            record.Subject,
            record.TextBody,
            record.HtmlBody,
            TemplateVariables = MessageMapper.SerializeJson(record.TemplateVariables),
            record.IdempotencyKey,
            record.ReplyToMessageId,
            record.InReplyTo,
            record.ReferencesHeader
        };
    }

    private sealed class MessageInsertRow
    {
        public Guid Id { get; init; }
        public bool Inserted { get; init; }
    }
}
