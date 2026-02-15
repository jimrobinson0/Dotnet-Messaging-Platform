using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageReader
{
    private const string MessageColumns = """
                                            m.id as Id,
                                            m.channel as Channel,
                                            m.status::text as Status,
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
                                                from message_participants p
                                                where p.message_id = @MessageId
                                                order by p.created_at
                                                """;

    private const string ParticipantSelectByIdsSql = """
                                                     select
                                                       p.id as Id,
                                                       p.message_id as MessageId,
                                                       p.role::text as Role,
                                                       p.address as Address,
                                                       p.display_name as DisplayName,
                                                       p.created_at as CreatedAt
                                                     from message_participants p
                                                     where p.message_id = any(@MessageIds)
                                                     order by p.message_id, p.created_at
                                                     """;

    private static readonly string MessageByIdSql = $"""
                                                     select
                                                     {MessageColumns}
                                                     from messages m
                                                     where m.id = @MessageId
                                                     """;

    private static readonly string MessageByIdForUpdateSql = $"""
                                                              select
                                                              {MessageColumns}
                                                              from messages m
                                                              where m.id = @MessageId
                                                              for update
                                                              """;

    private static readonly string ListSql = $"""
                                              select
                                              {MessageColumns}
                                              from messages m
                                              where (@Status is null or m.status = @Status::message_status)
                                                and (@CreatedAfter::timestamptz is null or m.created_at > @CreatedAfter)
                                              order by m.created_at
                                              limit @Limit
                                              """;

    private const string ReplyTargetSql = """
                                          select
                                            m.id as Id,
                                            m.status::text as Status,
                                            m.smtp_message_id as SmtpMessageId,
                                            m.references_header as ReferencesHeader
                                          from messages m
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
    ///     Lists messages within a transactional context with optional cursor-based pagination.
    /// </summary>
    public async Task<IReadOnlyList<Message>> ListAsync(
        MessageStatus? status,
        int limit,
        DateTimeOffset? createdAfter,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return await ListCoreAsync(
            status, limit, createdAfter, DbGuard.GetConnection(transaction), transaction, cancellationToken);
    }

    /// <summary>
    ///     Lists messages using a plain connection (no transaction overhead) with optional cursor-based pagination.
    ///     Suitable for read-only operations outside a unit of work.
    /// </summary>
    public async Task<IReadOnlyList<Message>> ListAsync(
        MessageStatus? status,
        int limit,
        DateTimeOffset? createdAfter,
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        return await ListCoreAsync(
            status, limit, createdAfter, connection, null, cancellationToken);
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

    private async Task<IReadOnlyList<Message>> ListCoreAsync(
        MessageStatus? status,
        int limit,
        DateTimeOffset? createdAfter,
        NpgsqlConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (limit <= 0) return Array.Empty<Message>();

        try
        {
            var rows = (await connection.QueryAsync<MessageRow>(
                    new CommandDefinition(
                        ListSql,
                        new
                        {
                            Status = status?.ToString(),
                            CreatedAfter = createdAfter,
                            Limit = limit
                        },
                        transaction,
                        cancellationToken: cancellationToken)))
                .ToList();

            if (rows.Count == 0) return Array.Empty<Message>();

            var messageIds = rows.Select(row => row.Id).ToArray();

            var participantRows = (await connection.QueryAsync<MessageParticipantRow>(
                    new CommandDefinition(
                        ParticipantSelectByIdsSql,
                        new { MessageIds = messageIds },
                        transaction,
                        cancellationToken: cancellationToken)))
                .ToList();

            var participantsByMessage = participantRows
                .GroupBy(row => row.MessageId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<MessageParticipantRow>)group.ToList());

            var messages = new List<Message>(rows.Count);
            foreach (var row in rows)
            {
                participantsByMessage.TryGetValue(row.Id, out var participants);
                participants ??= Array.Empty<MessageParticipantRow>();
                messages.Add(MessageMapper.RehydrateMessage(row, participants));
            }

            return messages;
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to list messages.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to list messages.", ex);
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
