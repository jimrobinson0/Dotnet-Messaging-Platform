using System.Data.Common;
using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Persistence.Reviews;

public sealed class ReviewWriter
{
    private const string InsertSql = """
                                     insert into core.message_reviews (
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
                                       @Decision::core.review_decision,
                                       @DecidedBy,
                                       @DecidedAt,
                                       @Notes
                                     );
                                     """;

    public async Task InsertAsync(MessageReview review, DbTransaction transaction,
        CancellationToken cancellationToken = default)
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
            await connection.ExecuteAsync(
                new CommandDefinition(InsertSql, parameters, transaction, cancellationToken: cancellationToken));
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