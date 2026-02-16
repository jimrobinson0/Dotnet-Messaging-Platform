using System.Text;
using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Persistence.Messages.Reads;

public sealed class MessageReadRepository(DbConnectionFactory connectionFactory) : IMessageReadRepository
{
    public async Task<PagedReadResult<MessageReadItem>> ListAsync(
        MessageReadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = new DynamicParameters();
        var whereClause = BuildWhereClause(query, parameters);

        parameters.Add("PageSize", query.PageSize);
        parameters.Add("Offset", query.Offset);

        var countSql = $"""
                        select count(1)
                        from core.messages m
                        where 1 = 1
                        {whereClause};
                        """;

        var listSql = $"""
                       select
                         m.id as Id,
                         m.channel as Channel,
                         m.status as Status,
                         m.requires_approval as RequiresApproval,
                         m.subject as Subject,
                         m.created_at as CreatedAt,
                         m.sent_at as SentAt,
                         m.failure_reason as FailureReason
                       from core.messages m
                       where 1 = 1
                       {whereClause}
                       order by m.created_at desc, m.id desc
                       limit @PageSize
                       offset @Offset;
                       """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        try
        {
            var totalCount = await connection.QuerySingleAsync<int>(
                new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

            var rows = (await connection.QueryAsync<MessageListRow>(
                    new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken)))
                .ToArray();

            var items = rows.Select(MapRow).ToArray();

            return new PagedReadResult<MessageReadItem>
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                Items = items
            };
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to list message summaries.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to list message summaries.", ex);
        }
    }

    private static string BuildWhereClause(
        MessageReadQuery query,
        DynamicParameters parameters)
    {
        var builder = new StringBuilder();

        if (query.Status is { Count: > 0 })
        {
            var statuses = query.Status
                .Distinct()
                .ToArray();
            parameters.Add("Status", statuses);
            builder.AppendLine("  and m.status = any(@Status)");
        }

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            parameters.Add("Channel", query.Channel.Trim());
            builder.AppendLine("  and m.channel = @Channel");
        }

        if (query.CreatedFrom.HasValue)
        {
            parameters.Add("CreatedFrom", query.CreatedFrom.Value);
            builder.AppendLine("  and m.created_at >= @CreatedFrom");
        }

        if (query.CreatedTo.HasValue)
        {
            parameters.Add("CreatedTo", query.CreatedTo.Value);
            builder.AppendLine("  and m.created_at < @CreatedTo");
        }

        if (query.SentFrom.HasValue)
        {
            parameters.Add("SentFrom", query.SentFrom.Value);
            builder.AppendLine("  and m.sent_at >= @SentFrom");
        }

        if (query.SentTo.HasValue)
        {
            parameters.Add("SentTo", query.SentTo.Value);
            builder.AppendLine("  and m.sent_at < @SentTo");
        }

        if (!query.RequiresApproval.HasValue) 
            return builder.ToString();
        
        parameters.Add("RequiresApproval", query.RequiresApproval.Value);
        builder.AppendLine("  and m.requires_approval = @RequiresApproval");

        return builder.ToString();
    }

    private static MessageReadItem MapRow(MessageListRow row)
    {
        return new MessageReadItem
        {
            Id = row.Id,
            Channel = row.Channel,
            Status = row.Status,
            RequiresApproval = row.RequiresApproval,
            Subject = row.Subject,
            CreatedAt = row.CreatedAt,
            SentAt = row.SentAt,
            FailureReason = row.FailureReason
        };
    }

    private sealed class MessageListRow
    {
        public Guid Id { get; set; }
        public string Channel { get; set; } = string.Empty;
        public MessageStatus Status { get; set; }
        public bool RequiresApproval { get; set; }
        public string? Subject { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public string? FailureReason { get; set; }
    }
}
