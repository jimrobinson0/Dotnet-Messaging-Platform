using Dapper;
using Messaging.Core;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Messages;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Messaging.Persistence.Tests.Infrastructure;
using Npgsql;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageClaimTests : PostgresTestBase
{
    public MessageClaimTests(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimNextApprovedAsync_claims_oldest_approved_message_and_sets_claim_metadata()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var pendingId = Guid.NewGuid();
        var oldestApprovedId = Guid.NewGuid();
        var newestApprovedId = Guid.NewGuid();

        var pendingCreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var oldestApprovedCreatedAt = pendingCreatedAt.AddMinutes(1);
        var newestApprovedCreatedAt = pendingCreatedAt.AddMinutes(2);

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, created_at, updated_at
                )
                values
                  (@PendingId, 'email', 'PendingApproval', 'Direct', 'Pending', 'Body', @PendingCreatedAt, @PendingCreatedAt),
                  (@OldestApprovedId, 'email', 'Approved'::core.message_status, 'Direct', 'Oldest', 'Body', @OldestApprovedCreatedAt, @OldestApprovedCreatedAt),
                  (@NewestApprovedId, 'email', 'Approved'::core.message_status, 'Direct', 'Newest', 'Body', @NewestApprovedCreatedAt, @NewestApprovedCreatedAt);
                """,
                new
                {
                    PendingId = pendingId,
                    OldestApprovedId = oldestApprovedId,
                    NewestApprovedId = newestApprovedId,
                    PendingCreatedAt = pendingCreatedAt,
                    OldestApprovedCreatedAt = oldestApprovedCreatedAt,
                    NewestApprovedCreatedAt = newestApprovedCreatedAt
                });
        }

        var claimed = await repository.ClaimNextApprovedAsync("worker-1");

        Assert.NotNull(claimed);
        Assert.Equal(oldestApprovedId, claimed!.Id);
        Assert.Equal(MessageStatus.Sending, claimed.Status);
        Assert.Equal("worker-1", claimed.ClaimedBy);
        Assert.NotNull(claimed.ClaimedAt);
        Assert.True(claimed.UpdatedAt >= claimed.ClaimedAt!.Value);
        Assert.True(claimed.UpdatedAt > oldestApprovedCreatedAt);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var newestStatus = await verifyConnection.QuerySingleAsync<string>(
            """
            select status::text
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = newestApprovedId });

        Assert.Equal("Approved", newestStatus);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimNextApprovedAsync_returns_null_when_only_non_approved_messages_exist()
    {
        await ResetDbAsync();

        var repository = CreateRepository();

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (id, channel, status, content_source, subject, text_body)
                values
                  (@PendingId, 'email', 'PendingApproval', 'Direct', 'Pending', 'Body'),
                  (@FailedId, 'email', 'Failed'::core.message_status, 'Direct', 'Failed', 'Body'),
                  (@RejectedId, 'email', 'Rejected'::core.message_status, 'Direct', 'Rejected', 'Body');
                """,
                new
                {
                    PendingId = Guid.NewGuid(),
                    FailedId = Guid.NewGuid(),
                    RejectedId = Guid.NewGuid()
                });
        }

        var claimed = await repository.ClaimNextApprovedAsync("worker-1");

        Assert.Null(claimed);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var sendingCount = await verifyConnection.QuerySingleAsync<int>(
            "select count(1) from core.messages where status = 'Sending'::core.message_status");

        Assert.Equal(0, sendingCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimNextApprovedAsync_when_created_at_ties_claims_lower_id_first()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var createdAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, created_at, updated_at
                )
                values
                  (@FirstId, 'email', 'Approved'::core.message_status, 'Direct', 'First', 'Body', @CreatedAt, @CreatedAt),
                  (@SecondId, 'email', 'Approved'::core.message_status, 'Direct', 'Second', 'Body', @CreatedAt, @CreatedAt);
                """,
                new
                {
                    FirstId = firstId,
                    SecondId = secondId,
                    CreatedAt = createdAt
                });
        }

        var firstClaim = await repository.ClaimNextApprovedAsync("worker-1");
        var secondClaim = await repository.ClaimNextApprovedAsync("worker-1");

        Assert.NotNull(firstClaim);
        Assert.NotNull(secondClaim);
        Assert.Equal(firstId, firstClaim!.Id);
        Assert.Equal(secondId, secondClaim!.Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimNextApprovedAsync_does_not_double_claim_on_sequential_calls()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var approvedMessageId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (id, channel, status, content_source, subject, text_body)
                values (@MessageId, 'email', 'Approved'::core.message_status, 'Direct', 'Approved', 'Body');
                """,
                new { MessageId = approvedMessageId });
        }

        var firstClaim = await repository.ClaimNextApprovedAsync("worker-1");
        var secondClaim = await repository.ClaimNextApprovedAsync("worker-2");

        Assert.NotNull(firstClaim);
        Assert.Equal(approvedMessageId, firstClaim!.Id);
        Assert.Null(secondClaim);
        Assert.Equal("worker-1", firstClaim.ClaimedBy);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimNextApprovedAsync_does_not_double_claim_on_concurrent_calls()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (id, channel, status, content_source, subject, text_body)
                values (@MessageId, 'email', 'Approved'::core.message_status, 'Direct', 'Approved', 'Body');
                """,
                new { MessageId = messageId });
        }

        var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var claimA = Task.Run(async () =>
        {
            await startGate.Task;
            return await CreateRepository().ClaimNextApprovedAsync("worker-a");
        });

        var claimB = Task.Run(async () =>
        {
            await startGate.Task;
            return await CreateRepository().ClaimNextApprovedAsync("worker-b");
        });

        startGate.SetResult(true);
        var results = await Task.WhenAll(claimA, claimB);
        var claimed = results.Where(message => message is not null).ToArray();
        var unclaimed = results.Where(message => message is null).ToArray();

        Assert.Single(claimed);
        Assert.Single(unclaimed);
        Assert.Equal(messageId, claimed[0]!.Id);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var row = await verifyConnection.QuerySingleAsync<(string Status, string ClaimedBy)>(
            """
            select
              status::text as Status,
              claimed_by as ClaimedBy
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = messageId });

        Assert.Equal("Sending", row.Status);
        Assert.Contains(row.ClaimedBy, new[] { "worker-a", "worker-b" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimNextApprovedAsync_does_not_change_attempt_count()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var messageId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, attempt_count
                )
                values (
                  @MessageId, 'email', 'Approved'::core.message_status, 'Direct', 'Approved', 'Body', 2
                );
                """,
                new { MessageId = messageId });
        }

        var claim = await repository.ClaimNextApprovedAsync("worker-1");

        Assert.NotNull(claim);
        Assert.Equal(messageId, claim!.Id);
        Assert.Equal(MessageStatus.Sending, claim.Status);
        Assert.Equal(2, claim.AttemptCount);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var attemptCount = await verifyConnection.QuerySingleAsync<int>(
            """
            select attempt_count
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = messageId });

        Assert.Equal(2, attemptCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Idempotent_replay_after_claim_does_not_reset_claim_state()
    {
        await ResetDbAsync();

        const string idempotencyKey = "claim-replay-key";
        var repository = CreateRepository();
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        (Guid MessageId, bool Inserted) firstInsert;
        var firstMessage = TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            firstInsert = await messageWriter.InsertIdempotentAsync(
                InsertMessageRecordMapper.ToInsertRecord(firstMessage, false),
                uow.Transaction);
            await uow.CommitAsync();
        }

        var claimed = await repository.ClaimNextApprovedAsync("worker-1");
        Assert.NotNull(claimed);
        Assert.Equal(firstInsert.MessageId, claimed!.Id);
        Assert.Equal(MessageStatus.Sending, claimed.Status);

        await Task.Delay(25);

        (Guid MessageId, bool Inserted) replayInsert;
        var replayMessage = TestData.CreatePendingApprovalMessage(Guid.NewGuid(), idempotencyKey);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            replayInsert = await messageWriter.InsertIdempotentAsync(
                InsertMessageRecordMapper.ToInsertRecord(replayMessage, true),
                uow.Transaction);
            await uow.CommitAsync();
        }

        Assert.False(replayInsert.Inserted);
        Assert.Equal(firstInsert.MessageId, replayInsert.MessageId);

        var afterReplay = await repository.GetByIdAsync(firstInsert.MessageId);
        Assert.Equal(MessageStatus.Sending, afterReplay.Status);
        Assert.Equal("worker-1", afterReplay.ClaimedBy);
        Assert.Equal(claimed.ClaimedAt, afterReplay.ClaimedAt);
        Assert.True(afterReplay.UpdatedAt > claimed.UpdatedAt);
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
