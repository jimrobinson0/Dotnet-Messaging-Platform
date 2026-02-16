using Dapper;
using Messaging.Core;
using Messaging.Core.Audit;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Messages.Writes;
using Messaging.Persistence.Tests.Infrastructure;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageAuditPersistenceTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_audit_event_persists_row()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using var uow = await UnitOfWork.BeginAsync(connectionFactory);
        // Insert parent message to satisfy FK constraint.
        var message = TestData.CreatePendingApprovalMessage(messageId);
        var insertResult =
            await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                uow.Transaction);
        Assert.True(insertResult.Inserted);
        Assert.NotEqual(Guid.Empty, insertResult.MessageId);
        var persistedMessageId = insertResult.MessageId;

        var evt = TestData.CreateAuditEvent(
            persistedMessageId,
            null,
            MessageStatus.PendingApproval,
            AuditEventType.MessageCreated);

        var persistedEvent = await auditWriter.InsertAsync(evt, uow.Transaction);
        Assert.NotEqual(DateTimeOffset.MinValue, persistedEvent.OccurredAt);

        // Validate row exists via direct query (tests persistence contract).
        var count = await uow.Connection.QuerySingleAsync<int>(
            "select count(1) from core.message_audit_events where message_id = @MessageId",
            new { MessageId = persistedMessageId },
            uow.Transaction);

        Assert.Equal(1, count);

        await uow.CommitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_audit_event_with_status_transition_persists_from_and_to()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using var uow = await UnitOfWork.BeginAsync(connectionFactory);
        var message = TestData.CreatePendingApprovalMessage(messageId);
        var insertResult =
            await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                uow.Transaction);
        Assert.True(insertResult.Inserted);
        Assert.NotEqual(Guid.Empty, insertResult.MessageId);
        var persistedMessageId = insertResult.MessageId;

        var evt = TestData.CreateAuditEvent(
            persistedMessageId,
            MessageStatus.PendingApproval,
            MessageStatus.Approved,
            AuditEventType.MessageApproved);

        var persistedEvent = await auditWriter.InsertAsync(evt, uow.Transaction);
        Assert.NotEqual(DateTimeOffset.MinValue, persistedEvent.OccurredAt);

        var row = await uow.Connection.QuerySingleAsync<dynamic>(
            """
            select from_status::text as FromStatus, to_status::text as ToStatus
            from core.message_audit_events
            where message_id = @MessageId
            """,
            new { MessageId = persistedMessageId },
            uow.Transaction);

        Assert.Equal("PendingApproval", (string)row.fromstatus);
        Assert.Equal("Approved", (string)row.tostatus);

        await uow.CommitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Multiple_audit_events_for_same_message_are_all_persisted()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using var uow = await UnitOfWork.BeginAsync(connectionFactory);
        var message = TestData.CreatePendingApprovalMessage(messageId);
        var insertResult =
            await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                uow.Transaction);
        Assert.True(insertResult.Inserted);
        Assert.NotEqual(Guid.Empty, insertResult.MessageId);
        var persistedMessageId = insertResult.MessageId;

        var createdAudit = await auditWriter.InsertAsync(
            TestData.CreateAuditEvent(persistedMessageId, null, MessageStatus.PendingApproval, AuditEventType.MessageCreated),
            uow.Transaction);
        Assert.NotEqual(DateTimeOffset.MinValue, createdAudit.OccurredAt);

        var approvedAudit = await auditWriter.InsertAsync(
            TestData.CreateAuditEvent(persistedMessageId, MessageStatus.PendingApproval, MessageStatus.Approved,
                AuditEventType.MessageApproved),
            uow.Transaction);
        Assert.NotEqual(DateTimeOffset.MinValue, approvedAudit.OccurredAt);

        var count = await uow.Connection.QuerySingleAsync<int>(
            "select count(1) from core.message_audit_events where message_id = @MessageId",
            new { MessageId = persistedMessageId },
            uow.Transaction);

        Assert.Equal(2, count);

        await uow.CommitAsync();
    }
}
