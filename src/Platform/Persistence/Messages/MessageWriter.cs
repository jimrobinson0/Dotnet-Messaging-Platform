using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageWriter
{
    private const string InsertIdempotentSql = """
        insert into messages (
          id,
          channel,
          status,
          content_source,
          claimed_by,
          claimed_at,
          sent_at,
          failure_reason,
          attempt_count,
          template_key,
          template_version,
          template_resolved_at,
          subject,
          text_body,
          html_body,
          template_variables,
          idempotency_key
        )
        values (
          @Id,
          @Channel,
          @Status::message_status,
          @ContentSource::message_content_source,
          @ClaimedBy,
          @ClaimedAt,
          @SentAt,
          @FailureReason,
          @AttemptCount,
          @TemplateKey,
          @TemplateVersion,
          @TemplateResolvedAt,
          @Subject,
          @TextBody,
          @HtmlBody,
          @TemplateVariables::jsonb,
          @IdempotencyKey
        )
        on conflict (idempotency_key) where (idempotency_key is not null)
        do update /* DO UPDATE ... RETURNING to ensure MVCC-safe idempotent inserts */
            set idempotency_key = excluded.idempotency_key
        returning id;
        """;

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
        Message message,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var connection = DbGuard.GetConnection(transaction);
        var parameters = BuildInsertParameters(message);

        try
        {
            var insertedId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    InsertIdempotentSql,
                    parameters,
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (insertedId == null)
            {
                throw new PersistenceException(
                    $"Idempotent insert returned no message id for message '{message.Id}'.");
            }

            var messageId = insertedId.Value;

            return new MessageInsertResult(
                MessageId: messageId,
                WasCreated: messageId == message.Id);
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

    public async Task UpdateAsync(Message message, DbTransaction transaction, CancellationToken cancellationToken = default)
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
                new CommandDefinition(UpdateSql, parameters, transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0)
            {
                throw new NotFoundException($"Message '{message.Id}' was not found.");
            }
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

    private static object BuildInsertParameters(Message message)
    {
        return new
        {
            message.Id,
            message.Channel,
            Status = message.Status.ToString(),
            ContentSource = message.ContentSource.ToString(),
            message.ClaimedBy,
            message.ClaimedAt,
            message.SentAt,
            message.FailureReason,
            message.AttemptCount,
            message.TemplateKey,
            message.TemplateVersion,
            message.TemplateResolvedAt,
            message.Subject,
            message.TextBody,
            message.HtmlBody,
            TemplateVariables = MessageMapper.SerializeJson(message.TemplateVariables),
            message.IdempotencyKey
        };
    }

}
