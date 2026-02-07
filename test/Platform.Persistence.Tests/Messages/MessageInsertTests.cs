using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Messages;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Tests.Infrastructure;
using Xunit;

namespace Messaging.Platform.Persistence.Tests.Messages;

public sealed class MessageInsertTests : PostgresTestBase
{
    public MessageInsertTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Insert_and_rehydrate_message_round_trips_fields_and_participants()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var participantWriter = new ParticipantWriter();
            var auditWriter = new AuditWriter();

            await messageWriter.InsertAsync(message, uow.Transaction);
            await participantWriter.InsertAsync(message.Participants, uow.Transaction);

            await auditWriter.InsertAsync(
                TestData.CreateAuditEvent(messageId, null, message.Status, "MessageCreated"),
                uow.Transaction);

            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(messageId, uow.Transaction);

            Assert.Equal(messageId, loaded.Id);
            Assert.Equal("email", loaded.Channel);
            Assert.Equal(message.Status, loaded.Status);
            Assert.Equal(message.ContentSource, loaded.ContentSource);
            Assert.Equal(0, loaded.AttemptCount);
            Assert.Equal("Subject", loaded.Subject);
            Assert.Equal("Hello", loaded.TextBody);
            Assert.Null(loaded.HtmlBody);

            Assert.Equal(2, loaded.Participants.Count);
            Assert.Equal(1, loaded.Participants.Count(p => p.Role == Messaging.Platform.Core.MessageParticipantRole.Sender));
            Assert.Equal(1, loaded.Participants.Count(p => p.Role == Messaging.Platform.Core.MessageParticipantRole.To));

            // DB owns timestamps; we just assert they are populated.
            Assert.NotEqual(default, loaded.CreatedAt);
            Assert.NotEqual(default, loaded.UpdatedAt);
        }
    }

    [Fact]
    public async Task Insert_approved_message_persists_correct_status()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreateApprovedMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var participantWriter = new ParticipantWriter();

            await messageWriter.InsertAsync(message, uow.Transaction);
            await participantWriter.InsertAsync(message.Participants, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(messageId, uow.Transaction);

            Assert.Equal(Messaging.Platform.Core.MessageStatus.Approved, loaded.Status);
            Assert.Equal("Auto-approved subject", loaded.Subject);
            Assert.Equal("Auto-approved body", loaded.TextBody);
            Assert.Equal("<p>Auto-approved</p>", loaded.HtmlBody);
        }
    }

    [Fact]
    public async Task Insert_template_message_round_trips_template_fields_and_variables()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreateTemplateMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var participantWriter = new ParticipantWriter();

            await messageWriter.InsertAsync(message, uow.Transaction);
            await participantWriter.InsertAsync(message.Participants, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(messageId, uow.Transaction);

            Assert.Equal(Messaging.Platform.Core.MessageContentSource.Template, loaded.ContentSource);
            Assert.Equal("welcome-email", loaded.TemplateKey);
            Assert.Equal("2.1", loaded.TemplateVersion);
            Assert.NotNull(loaded.TemplateResolvedAt);

            // Validate JSONB round-trip.
            Assert.NotNull(loaded.TemplateVariables);
            Assert.Equal("Test", loaded.TemplateVariables.Value.GetProperty("name").GetString());
            Assert.Equal(42, loaded.TemplateVariables.Value.GetProperty("orderId").GetInt32());
        }
    }

    [Fact]
    public async Task Insert_message_without_participants_round_trips_with_empty_list()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreateMessageWithoutParticipants(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            await messageWriter.InsertAsync(message, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(messageId, uow.Transaction);

            Assert.Equal(messageId, loaded.Id);
            Assert.Equal("email", loaded.Channel);
            Assert.Empty(loaded.Participants);
        }
    }

    [Fact]
    public async Task Insert_duplicate_message_throws_ConcurrencyException()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            await messageWriter.InsertAsync(message, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var duplicate = TestData.CreatePendingApprovalMessage(messageId);

            await Assert.ThrowsAsync<Messaging.Platform.Persistence.Exceptions.ConcurrencyException>(
                () => messageWriter.InsertAsync(duplicate, uow.Transaction));
        }
    }

    [Fact]
    public async Task GetByIdAsync_for_nonexistent_id_throws_NotFoundException()
    {
        await ResetDbAsync();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using var uow = await UnitOfWork.BeginAsync(connectionFactory);

        var reader = new MessageReader();
        await Assert.ThrowsAsync<Messaging.Platform.Persistence.Exceptions.NotFoundException>(
            () => reader.GetByIdAsync(Guid.NewGuid(), uow.Transaction));
    }
}
