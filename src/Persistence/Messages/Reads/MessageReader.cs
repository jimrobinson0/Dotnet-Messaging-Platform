using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Persistence.Messages;

public sealed class MessageReader
{
    private const string MessageColumns = """
                                            m.id as Id,
                                            m.channel as Channel,
                                            m.status as Status,
                                            m.content_source::text as ContentSource,
                                            m.created_at as CreatedAt,
                                            m.updated_at as UpdatedAt,
                                            m.claimed_by as ClaimedBy,
                                            m.claimed_at as ClaimedAt,
                                            m.sent_at as SentAt,
                                            m.failure_reason as FailureReason,
                                            m.attempt_count as AttemptCount,
                                            m.template_key as TemplateKey,
                                            m.template_version as TemplateVersion,
                                            m.template_resolved_at as TemplateResolvedAt,
                                            m.subject as Subject,
                                            m.text_body as TextBody,
                                            m.html_body as HtmlBody,
                                            m.template_variables::text as TemplateVariablesJson,
                                            m.idempotency_key as IdempotencyKey,
                                            m.reply_to_message_id as ReplyToMessageId,
                                            m.in_reply_to as InReplyTo,
                                            m.references_header as ReferencesHeader,
                                            m.smtp_message_id as SmtpMessageId
                                          """;

    private const string ParticipantSelectSql = """
                                                select
                                                  p.id as Id,
                                                  p.message_id as MessageId,
                                                  p.role::text as Role,
                                                  p.address as Address,
                                                  p.display_name as DisplayName,
                                                  p.created_at as CreatedAt
                                                from core.message_participants p
                                                where p.message_id = @MessageId
                                                order by p.created_at
                                                """;

    private static readonly string MessageByIdSql = $"""
                                                     select
                                                     {MessageColumns}
                                                     from core.messages m
                                                     where m.id = @MessageId
                                                     """;

    private static readonly string MessageByIdForUpdateSql = $"""
                                                              select
                                                              {MessageColumns}
                                                              from core.messages m
                                                              where m.id = @MessageId
                                                              for update
                                                              """;

    private const string ReplyTargetSql = """
                                          select
                                            m.id as Id,
                                            m.status as Status,
                                            m.smtp_message_id as SmtpMessageId,
                                            m.references_header as ReferencesHeader
                                          from core.messages m
                                          where m.id = @MessageId
                                          """;

    /// <summary>
    ///     Loads a message by ID within a transactional context.
    /// </summary>
    public async Task<Message> GetByIdAsync(
        Guid messageId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return await GetByIdCoreAsync(
            messageId, MessageByIdSql, DbGuard.GetConnection(transaction), transaction, cancellationToken);
    }

    /// <summary>
    ///     Loads a message by ID using a plain connection (no transaction overhead).
    ///     Suitable for read-only operations outside a unit of work.
    /// </summary>
    public async Task<Message> GetByIdAsync(
        Guid messageId,
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        return await GetByIdCoreAsync(
            messageId, MessageByIdSql, connection, null, cancellationToken);
    }

    /// <summary>
    ///     Loads a message by ID with a row-level lock (FOR UPDATE).
    ///     Must be called within a transaction.
    /// </summary>
    public async Task<Message> GetForUpdateAsync(
        Guid messageId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return await GetByIdCoreAsync(
            messageId, MessageByIdForUpdateSql, DbGuard.GetConnection(transaction), transaction, cancellationToken);
    }

    /// <summary>
    ///     Loads minimal reply-threading state for a message.
    ///     Returns null when the target message does not exist.
    /// </summary>
    internal async Task<ReplyTargetRow?> GetReplyTargetAsync(
        Guid messageId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var connection = DbGuard.GetConnection(transaction);

        try
        {
            return await connection.QuerySingleOrDefaultAsync<ReplyTargetRow>(
                new CommandDefinition(
                    ReplyTargetSql,
                    new { MessageId = messageId },
                    transaction,
                    cancellationToken: cancellationToken));
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to load reply target.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to load reply target.", ex);
        }
    }

    private async Task<Message> GetByIdCoreAsync(
        Guid messageId,
        string sql,
        NpgsqlConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<MessageRow>(
                new CommandDefinition(sql, new { MessageId = messageId }, transaction,
                    cancellationToken: cancellationToken));

            if (row is null) throw new NotFoundException($"Message '{messageId}' was not found.");

            var participants = (await connection.QueryAsync<MessageParticipantRow>(
                    new CommandDefinition(ParticipantSelectSql, new { MessageId = messageId }, transaction,
                        cancellationToken: cancellationToken)))
                .ToList();

            return MessageMapper.RehydrateMessage(row, participants);
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to load message.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to load message.", ex);
        }
    }

}

internal sealed class ReplyTargetRow
{
    public Guid Id { get; set; }
    public MessageStatus Status { get; set; }
    public string? SmtpMessageId { get; set; }
    public string? ReferencesHeader { get; set; }
}
