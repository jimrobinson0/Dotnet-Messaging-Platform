using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Participants;

public sealed class ParticipantWriter
{
    private const string InsertSql = """
        insert into message_participants (
          id,
          message_id,
          role,
          address,
          display_name
        )
        values (
          @Id,
          @MessageId,
          @Role::message_participant_role,
          @Address,
          @DisplayName
        );
        """;

    public async Task InsertAsync(IEnumerable<MessageParticipant> participants, DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var connection = DbGuard.GetConnection(transaction);

        var rows = participants
            .Select(participant => new
            {
                participant.Id,
                participant.MessageId,
                Role = participant.Role.ToString(),
                participant.Address,
                participant.DisplayName
            })
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        try
        {
            await connection.ExecuteAsync(InsertSql, rows, transaction);
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to insert participants.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to insert participants.", ex);
        }
    }

}
