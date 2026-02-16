using System.Text.Json;
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

        await auditWriter.InsertAsync(evt, uow.Transaction);

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

        await auditWriter.InsertAsync(evt, uow.Transaction);

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
    public async Task Insert_audit_event_with_metadata_json_round_trips()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var metadata = JsonDocument.Parse("""{"reason":"retry_exceeded","attempt":3}""").RootElement;

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
            null,
            null,
            AuditEventType.FailureRecorded,
            metadata);

        await auditWriter.InsertAsync(evt, uow.Transaction);

        var raw = await uow.Connection.QuerySingleAsync<string>(
            "select metadata_json::text from core.message_audit_events where message_id = @MessageId",
            new { MessageId = persistedMessageId },
            uow.Transaction);

        Assert.NotNull(raw);
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal("retry_exceeded", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("attempt").GetInt32());

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

        await auditWriter.InsertAsync(
            TestData.CreateAuditEvent(persistedMessageId, null, MessageStatus.PendingApproval, AuditEventType.MessageCreated),
            uow.Transaction);

        await auditWriter.InsertAsync(
            TestData.CreateAuditEvent(persistedMessageId, MessageStatus.PendingApproval, MessageStatus.Approved,
                AuditEventType.MessageApproved),
            uow.Transaction);

        var count = await uow.Connection.QuerySingleAsync<int>(
            "select count(1) from core.message_audit_events where message_id = @MessageId",
            new { MessageId = persistedMessageId },
            uow.Transaction);

        Assert.Equal(2, count);

        await uow.CommitAsync();
    }
}