using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageWriter
{
    private const string InsertSql = """
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
          template_variables
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
          @TemplateVariables::jsonb
        );
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

    public async Task InsertAsync(Message message, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var connection = DbGuard.GetConnection(transaction);

        var parameters = new
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
            TemplateVariables = MessageMapper.SerializeJson(message.TemplateVariables)
        };

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(InsertSql, parameters, transaction: transaction, cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConcurrencyException($"Message '{message.Id}' already exists.", ex);
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to insert message.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to insert message.", ex);
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

}
