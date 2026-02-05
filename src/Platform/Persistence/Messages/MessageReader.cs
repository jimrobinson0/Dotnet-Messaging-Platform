using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Messages;

public sealed class MessageReader
{
    private const string MessageSelectSql = """
        select
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
          m.template_variables::text as TemplateVariablesJson
        from messages m
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

    public async Task<Message> GetByIdAsync(Guid messageId, DbTransaction transaction)
    {
        var connection = DbGuard.GetConnection(transaction);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<MessageRow>(
                MessageSelectSql + "where m.id = @MessageId",
                new { MessageId = messageId },
                transaction);

            if (row is null)
            {
                throw new NotFoundException($"Message '{messageId}' was not found.");
            }

            var participants = (await connection.QueryAsync<MessageParticipantRow>(
                ParticipantSelectSql,
                new { MessageId = messageId },
                transaction)).ToList();

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

    public async Task<Message> GetForUpdateAsync(Guid messageId, DbTransaction transaction)
    {
        var connection = DbGuard.GetConnection(transaction);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<MessageRow>(
                MessageSelectSql + "where m.id = @MessageId for update",
                new { MessageId = messageId },
                transaction);

            if (row is null)
            {
                throw new NotFoundException($"Message '{messageId}' was not found.");
            }

            var participants = (await connection.QueryAsync<MessageParticipantRow>(
                ParticipantSelectSql,
                new { MessageId = messageId },
                transaction)).ToList();

            return MessageMapper.RehydrateMessage(row, participants);
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to load message for update.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to load message for update.", ex);
        }
    }

    public async Task<IReadOnlyList<Message>> ListPendingApprovalAsync(int limit, DbTransaction transaction)
    {
        if (limit <= 0)
        {
            return Array.Empty<Message>();
        }

        var connection = DbGuard.GetConnection(transaction);

        try
        {
            var rows = (await connection.QueryAsync<MessageRow>(
                MessageSelectSql + "where m.status = @Status::message_status order by m.created_at limit @Limit",
                new
                {
                    Status = MessageStatus.PendingApproval.ToString(),
                    Limit = limit
                },
                transaction)).ToList();

            if (rows.Count == 0)
            {
                return Array.Empty<Message>();
            }

            var messageIds = rows.Select(row => row.Id).ToArray();

            var participantRows = (await connection.QueryAsync<MessageParticipantRow>(
                ParticipantSelectByIdsSql,
                new { MessageIds = messageIds },
                transaction)).ToList();

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
            throw new PersistenceException("Failed to list pending approval messages.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to list pending approval messages.", ex);
        }
    }

}
