using System.Data.Common;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Reviews;

public sealed class ReviewWriter
{
    private const string InsertSql = """
        insert into message_reviews (
          id,
          message_id,
          decision,
          decided_by,
          decided_at,
          notes
        )
        values (
          @Id,
          @MessageId,
          @Decision::review_decision,
          @DecidedBy,
          @DecidedAt,
          @Notes
        );
        """;

    public async Task InsertAsync(MessageReview review, DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(review);
        var connection = DbGuard.GetConnection(transaction);

        var parameters = new
        {
            review.Id,
            review.MessageId,
            Decision = review.Decision.ToString(),
            review.DecidedBy,
            review.DecidedAt,
            review.Notes
        };

        try
        {
            await connection.ExecuteAsync(InsertSql, parameters, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConcurrencyException(
                $"Review for message '{review.MessageId}' already exists.",
                ex);
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to insert review.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to insert review.", ex);
        }
    }

}
