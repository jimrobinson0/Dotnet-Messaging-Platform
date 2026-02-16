using System.Collections.Concurrent;
using Dapper;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Messages;
using Messaging.Persistence.Messages.Reads;
using Messaging.Persistence.Messages.Writes;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Messaging.Persistence.Tests.Infrastructure;
using Npgsql;

namespace Messaging.Persistence.Tests.Messages;

public sealed class IdempotentInsertConcurrencyTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_inserts_with_same_idempotency_key_result_in_single_row()
    {
        await ResetDbAsync();

        const int parallel = 8;
        const string idempotencyKey = "fixed-key";
        var results = new ConcurrentBag<(Guid Id, bool Inserted)>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, parallel),
            async (_, cancellationToken) =>
            {
                var message = TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey);
                var repository = CreateRepository();

                var (persisted, inserted) = await repository.InsertAsync(
                    message,
                    false,
                    "System",
                    "test",
                    cancellationToken);

                results.Add((persisted.Id, inserted));
            });

        var ids = results.Select(result => result.Id).Distinct().ToList();

        Assert.Single(ids);
        Assert.Equal(1, results.Count(result => result.Inserted));
        Assert.Equal(parallel - 1, results.Count(result => !result.Inserted));

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var rowCount = await connection.QuerySingleAsync<int>(
            """
            select count(1)
            from core.messages
            where idempotency_key = @IdempotencyKey
            """,
            new { IdempotencyKey = idempotencyKey });

        Assert.Equal(1, rowCount);
    }

    private MessageRepository CreateRepository()
    {
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        return new MessageRepository(
            connectionFactory,
            new MessageReader(),
            new MessageWriter(),
            new ParticipantWriter(),
            new ReviewWriter(),
            new AuditWriter());
    }
}
