using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Persistence.Messages;

public sealed class MessageWriter
{
    // Idempotent insert contract:
    // - Always returns a row.
    // - Id is newly inserted id or existing id for idempotency replay.
    // - Inserted is true when a new row was inserted.
    internal const string InsertIdempotentSql = @"
        insert into core.messages (
          channel, status, requires_approval, content_source, template_key, template_version,
          template_resolved_at, subject, text_body, html_body,
          template_variables, idempotency_key, reply_to_message_id, in_reply_to, references_header
        )
        values (
          @Channel, @Status::core.message_status, @RequiresApproval, @ContentSource::core.message_content_source,
          @TemplateKey, @TemplateVersion, @TemplateResolvedAt,
          @Subject, @TextBody, @HtmlBody, @TemplateVariables::jsonb, @IdempotencyKey,
          @ReplyToMessageId, @InReplyTo, @ReferencesHeader
        )
        on conflict (idempotency_key) where (idempotency_key is not null)
        do update
          set updated_at = now()
        returning
          id as Id,
          (xmax = 0) as inserted;
        ";

    private const string UpdateSql = """
                                     update core.messages
                                     set
                                       status = @Status::core.message_status,
                                       updated_at = now(),
                                       claimed_by = @ClaimedBy,
                                       claimed_at = @ClaimedAt,
                                       sent_at = @SentAt,
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
            var row = await connection.QuerySingleAsync<MessageInsertRow>(
                new CommandDefinition(
                    InsertIdempotentSql,
                    parameters,
                    transaction,
                    cancellationToken: cancellationToken));

            if (row.Id == Guid.Empty)
                throw new PersistenceException(
                    $"Message insert returned empty id; idempotencyKey={record.IdempotencyKey ?? "<null>"}");

            return (row.Id, row.Inserted);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConcurrencyException(
                "A uniqueness constraint was violated while inserting the message.",
                ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to insert message idempotently.", ex);
        }
    }

    public async Task UpdateAsync(Message message, DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var connection = DbGuard.GetConnection(transaction);

        var parameters = new
        {
            message.Id,
            Status = message.Status.ToString(),
            message.ClaimedBy,
            message.ClaimedAt,
            message.SentAt,
            message.FailureReason,
            message.AttemptCount
        };

        try
        {
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(UpdateSql, parameters, transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new NotFoundException($"Message '{message.Id}' was not found.");
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to update message.", ex);
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
