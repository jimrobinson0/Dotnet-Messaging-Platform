using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Persistence.Audit;

public sealed class AuditWriter
{
    private const string InsertSql = """
                                     insert into core.message_audit_events (
                                       id,
                                       message_id,
                                       event_type,
                                       from_status,
                                       to_status,
                                       actor_type,
                                       actor_id
                                     )
                                     values (
                                       @Id,
                                       @MessageId,
                                       @EventType,
                                       @FromStatus::core.message_status,
                                       @ToStatus::core.message_status,
                                       @ActorType,
                                       @ActorId
                                     )
                                     returning
                                       id as Id,
                                       occurred_at as OccurredAt;
                                     """;

    public async Task<MessageAuditEvent> InsertAsync(MessageAuditEvent auditEvent, DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var connection = DbGuard.GetConnection(transaction);

        var parameters = new
        {
            auditEvent.Id,
            auditEvent.MessageId,
            EventType = auditEvent.EventType.Value,
            FromStatus = auditEvent.FromStatus?.ToString(),
            ToStatus = auditEvent.ToStatus?.ToString(),
            auditEvent.ActorType,
            auditEvent.ActorId
        };

        try
        {
            var persisted = await connection.QuerySingleAsync<InsertedAuditRow>(
                new CommandDefinition(InsertSql, parameters, transaction, cancellationToken: cancellationToken));

            return new MessageAuditEvent(
                persisted.Id,
                auditEvent.MessageId,
                auditEvent.EventType,
                auditEvent.FromStatus,
                auditEvent.ToStatus,
                auditEvent.ActorType,
                auditEvent.ActorId,
                persisted.OccurredAt);
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to insert audit event.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to insert audit event.", ex);
        }
    }

    private sealed class InsertedAuditRow
    {
        public Guid Id { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
    }
}
