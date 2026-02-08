using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Messages;
using Messaging.Platform.Persistence.Tests.Infrastructure;
using Xunit;

namespace Messaging.Platform.Persistence.Tests.Audit;

public sealed class MessageAuditPersistenceTests : PostgresTestBase
{
    public MessageAuditPersistenceTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Insert_audit_event_persists_row()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            // Insert parent message to satisfy FK constraint.
            var message = TestData.CreatePendingApprovalMessage(messageId);
            var insertResult = await messageWriter.InsertIdempotentAsync(message, uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.Equal(messageId, insertResult.MessageId);

            var evt = TestData.CreateAuditEvent(
                messageId: messageId,
                fromStatus: null,
                toStatus: MessageStatus.PendingApproval,
                eventType: "MessageCreated");

            await auditWriter.InsertAsync(evt, uow.Transaction);

            // Validate row exists via direct query (tests persistence contract).
            var count = await uow.Connection.QuerySingleAsync<int>(
                "select count(1) from message_audit_events where message_id = @MessageId",
                new { MessageId = messageId },
                uow.Transaction);

            Assert.Equal(1, count);

            await uow.CommitAsync();
        }
    }

    [Fact]
    public async Task Insert_audit_event_with_status_transition_persists_from_and_to()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var message = TestData.CreatePendingApprovalMessage(messageId);
            var insertResult = await messageWriter.InsertIdempotentAsync(message, uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.Equal(messageId, insertResult.MessageId);

            var evt = TestData.CreateAuditEvent(
                messageId: messageId,
                fromStatus: MessageStatus.PendingApproval,
                toStatus: MessageStatus.Approved,
                eventType: "StatusTransition");

            await auditWriter.InsertAsync(evt, uow.Transaction);

            var row = await uow.Connection.QuerySingleAsync<dynamic>(
                """
                select from_status::text as FromStatus, to_status::text as ToStatus
                from message_audit_events
                where message_id = @MessageId
                """,
                new { MessageId = messageId },
                uow.Transaction);

            Assert.Equal("PendingApproval", (string)row.fromstatus);
            Assert.Equal("Approved", (string)row.tostatus);

            await uow.CommitAsync();
        }
    }

    [Fact]
    public async Task Insert_audit_event_with_metadata_json_round_trips()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var metadata = JsonDocument.Parse("""{"reason":"retry_exceeded","attempt":3}""").RootElement;

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var message = TestData.CreatePendingApprovalMessage(messageId);
            var insertResult = await messageWriter.InsertIdempotentAsync(message, uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.Equal(messageId, insertResult.MessageId);

            var evt = TestData.CreateAuditEvent(
                messageId: messageId,
                fromStatus: null,
                toStatus: null,
                eventType: "FailureRecorded",
                metadata: metadata);

            await auditWriter.InsertAsync(evt, uow.Transaction);

            var raw = await uow.Connection.QuerySingleAsync<string>(
                "select metadata_json::text from message_audit_events where message_id = @MessageId",
                new { MessageId = messageId },
                uow.Transaction);

            Assert.NotNull(raw);
            using var doc = JsonDocument.Parse(raw);
            Assert.Equal("retry_exceeded", doc.RootElement.GetProperty("reason").GetString());
            Assert.Equal(3, doc.RootElement.GetProperty("attempt").GetInt32());

            await uow.CommitAsync();
        }
    }

    [Fact]
    public async Task Multiple_audit_events_for_same_message_are_all_persisted()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var auditWriter = new AuditWriter();

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var message = TestData.CreatePendingApprovalMessage(messageId);
            var insertResult = await messageWriter.InsertIdempotentAsync(message, uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.Equal(messageId, insertResult.MessageId);

            await auditWriter.InsertAsync(
                TestData.CreateAuditEvent(messageId, null, MessageStatus.PendingApproval, "MessageCreated"),
                uow.Transaction);

            await auditWriter.InsertAsync(
                TestData.CreateAuditEvent(messageId, MessageStatus.PendingApproval, MessageStatus.Approved, "StatusTransition"),
                uow.Transaction);

            await auditWriter.InsertAsync(
                TestData.CreateAuditEvent(messageId, MessageStatus.Approved, MessageStatus.Sending, "StatusTransition"),
                uow.Transaction);

            var count = await uow.Connection.QuerySingleAsync<int>(
                "select count(1) from message_audit_events where message_id = @MessageId",
                new { MessageId = messageId },
                uow.Transaction);

            Assert.Equal(3, count);

            await uow.CommitAsync();
        }
    }
}
