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
                                     insert into message_audit_events (
                                       id,
                                       message_id,
                                       event_type,
                                       from_status,
                                       to_status,
                                       actor_type,
                                       actor_id,
                                       occurred_at,
                                       metadata_json
                                     )
                                     values (
                                       @Id,
                                       @MessageId,
                                       @EventType,
                                       @FromStatus::message_status,
                                       @ToStatus::message_status,
                                       @ActorType,
                                       @ActorId,
                                       @OccurredAt,
                                       @MetadataJson::jsonb
                                     );
                                     """;

    public async Task InsertAsync(MessageAuditEvent auditEvent, DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var connection = DbGuard.GetConnection(transaction);

        var parameters = new
        {
            auditEvent.Id,
            auditEvent.MessageId,
            auditEvent.EventType,
            FromStatus = auditEvent.FromStatus?.ToString(),
            ToStatus = auditEvent.ToStatus?.ToString(),
            auditEvent.ActorType,
            auditEvent.ActorId,
            auditEvent.OccurredAt,
            MetadataJson = auditEvent.MetadataJson?.GetRawText()
        };

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(InsertSql, parameters, transaction, cancellationToken: cancellationToken));
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
}