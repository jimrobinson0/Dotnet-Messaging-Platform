using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Messaging.Platform.Persistence.Messages;
using Npgsql;

namespace Messaging.Platform.Persistence.Audit;

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

    public async Task InsertAsync(MessageAuditEvent auditEvent, DbTransaction transaction)
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
            MetadataJson = MessageMapper.SerializeJson(auditEvent.MetadataJson)
        };

        try
        {
            await connection.ExecuteAsync(InsertSql, parameters, transaction);
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
