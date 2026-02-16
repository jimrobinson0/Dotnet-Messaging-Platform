using Dapper;
using Messaging.Core;
using Messaging.Core.Audit;
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

public sealed class MessageReviewApplyTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApplyReviewAsync_approve_records_single_transition_audit_with_db_timestamp()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var created = await repository.InsertAsync(
            TestData.CreatePendingApprovalMessage(Guid.NewGuid()),
            true,
            "System",
            "seed");

        var reviewed = await repository.ApplyReviewAsync(
            created.Message.Id,
            message => message.Approve(Guid.NewGuid(), "reviewer", "ok", ActorType.Human),
            AuditEventType.MessageApproved,
            "Human",
            "reviewer-1");

        Assert.Equal(MessageStatus.Approved, reviewed.Status);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var approvedAuditCount = await connection.QuerySingleAsync<int>(
            """
            select count(1)
            from core.message_audit_events
            where message_id = @MessageId
              and event_type = @EventType
            """,
            new
            {
                MessageId = created.Message.Id,
                EventType = AuditEventType.MessageApproved.Value
            });

        var transitionAudit = await connection.QuerySingleAsync<(string FromStatus, string ToStatus, DateTimeOffset OccurredAt)>(
            """
            select
              from_status::text as FromStatus,
              to_status::text as ToStatus,
              occurred_at as OccurredAt
            from core.message_audit_events
            where message_id = @MessageId
              and event_type = @EventType
            """,
            new
            {
                MessageId = created.Message.Id,
                EventType = AuditEventType.MessageApproved.Value
            });

        Assert.Equal(1, approvedAuditCount);
        Assert.Equal("PendingApproval", transitionAudit.FromStatus);
        Assert.Equal("Approved", transitionAudit.ToStatus);
        Assert.NotEqual(default, transitionAudit.OccurredAt);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApplyReviewAsync_reject_records_single_transition_audit_with_db_timestamp()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var created = await repository.InsertAsync(
            TestData.CreatePendingApprovalMessage(Guid.NewGuid()),
            true,
            "System",
            "seed");

        var reviewed = await repository.ApplyReviewAsync(
            created.Message.Id,
            message => message.Reject(Guid.NewGuid(), "reviewer", "no", ActorType.Human),
            AuditEventType.MessageRejected,
            "Human",
            "reviewer-1");

        Assert.Equal(MessageStatus.Rejected, reviewed.Status);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var rejectedAuditCount = await connection.QuerySingleAsync<int>(
            """
            select count(1)
            from core.message_audit_events
            where message_id = @MessageId
              and event_type = @EventType
            """,
            new
            {
                MessageId = created.Message.Id,
                EventType = AuditEventType.MessageRejected.Value
            });

        var transitionAudit = await connection.QuerySingleAsync<(string FromStatus, string ToStatus, DateTimeOffset OccurredAt)>(
            """
            select
              from_status::text as FromStatus,
              to_status::text as ToStatus,
              occurred_at as OccurredAt
            from core.message_audit_events
            where message_id = @MessageId
              and event_type = @EventType
            """,
            new
            {
                MessageId = created.Message.Id,
                EventType = AuditEventType.MessageRejected.Value
            });

        Assert.Equal(1, rejectedAuditCount);
        Assert.Equal("PendingApproval", transitionAudit.FromStatus);
        Assert.Equal("Rejected", transitionAudit.ToStatus);
        Assert.NotEqual(default, transitionAudit.OccurredAt);
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
