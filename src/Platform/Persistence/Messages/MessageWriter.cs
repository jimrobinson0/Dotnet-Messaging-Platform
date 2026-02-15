using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageWriter
{
    // Idempotent insert contract:
    // - Always returns exactly one row
    // - Id is either the newly inserted message id or the existing id for the idempotency key
    // - WasCreated is true iff a new row was inserted
    // - SQL is authoritative; C# must not attempt replay lookup or repair
    internal const string InsertIdempotentSql = @"
        insert into messages (
        channel, status, content_source, template_key, template_version,
        template_resolved_at, subject, text_body, html_body,
        template_variables, idempotency_key, reply_to_message_id, in_reply_to, references_header
        )
        values (
        @Channel, @Status::message_status, @ContentSource::message_content_source,
        @TemplateKey, @TemplateVersion, @TemplateResolvedAt,
        @Subject, @TextBody, @HtmlBody, @TemplateVariables::jsonb, @IdempotencyKey,
        @ReplyToMessageId, @InReplyTo, @ReferencesHeader
        )
        on conflict (idempotency_key) where (idempotency_key is not null)
        -- We intentionally bump updated_at on idempotent replay to record last persistence touch.
        -- Frozen content and reply metadata remain immutable.
        do update
        set updated_at = now()
        returning
        id as Id,
        (xmax = 0) as WasCreated;
        ";

    private const string UpdateSql = """
                                     update messages
                                     set
                                       status = @Status::message_status,
                                       updated_at = now(),
                                       claimed_by = @ClaimedBy,
                                       claimed_at = @ClaimedAt,
                                       sent_at = @SentAt,
                                       failure_reason = @FailureReason,
                                       attempt_count = @AttemptCount
                                     where id = @Id;
                                     """;

    public async Task<MessageInsertResult> InsertIdempotentAsync(
        MessageCreateIntent createIntent,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createIntent);
        var connection = DbGuard.GetConnection(transaction);
        var parameters = BuildInsertParameters(createIntent);

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
                    $"Message insert returned empty id; idempotencyKey={createIntent.IdempotencyKey ?? "<null>"}");

            return new MessageInsertResult(
                row.Id,
                row.WasCreated);
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

    private static object BuildInsertParameters(MessageCreateIntent createIntent)
    {
        return new
        {
            createIntent.Channel,
            Status = createIntent.Status.ToString(),
            ContentSource = createIntent.ContentSource.ToString(),
            createIntent.TemplateKey,
            createIntent.TemplateVersion,
            createIntent.TemplateResolvedAt,
            createIntent.Subject,
            createIntent.TextBody,
            createIntent.HtmlBody,
            TemplateVariables = MessageMapper.SerializeJson(createIntent.TemplateVariables),
            createIntent.IdempotencyKey,
            createIntent.ReplyToMessageId,
            createIntent.InReplyTo,
            createIntent.ReferencesHeader
        };
    }

    private sealed class MessageInsertRow
    {
        public Guid Id { get; set; }
        public bool WasCreated { get; set; }
    }
}
