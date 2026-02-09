using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Messaging.Platform.Persistence.Messages;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Tests.Infrastructure;

namespace Messaging.Platform.Persistence.Tests.Messages;

public sealed class MessageUpdateTests : PostgresTestBase
{
    public MessageUpdateTests(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Update_persists_status_and_updated_at_changes()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reader = new MessageReader();
        Guid persistedMessageId;

        // Insert + participants
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message),
                    uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        DateTimeOffset firstUpdatedAt;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetByIdAsync(persistedMessageId, uow.Transaction);
            firstUpdatedAt = loaded.UpdatedAt;
        }

        // Ensure updated_at advances (DB-owned). Micro delay is enough for monotonicity.
        await Task.Delay(25);

        // Transition in Core, then persist.
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetForUpdateAsync(persistedMessageId, uow.Transaction);
            loaded.Cancel(DateTimeOffset.UtcNow);

            await messageWriter.UpdateAsync(loaded, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var after = await reader.GetByIdAsync(persistedMessageId, uow.Transaction);

            Assert.Equal(MessageStatus.Canceled, after.Status);
            Assert.True(after.UpdatedAt > firstUpdatedAt,
                $"Expected updated_at to increase. Before={firstUpdatedAt:o}, After={after.UpdatedAt:o}");
        }
    }

    [Fact]
    public async Task Update_persists_claimed_by_and_claimed_at_after_StartSending()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreateApprovedMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reader = new MessageReader();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message),
                    uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        var claimedAt = DateTimeOffset.UtcNow;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetForUpdateAsync(persistedMessageId, uow.Transaction);
            loaded.StartSending("worker-1", claimedAt);

            await messageWriter.UpdateAsync(loaded, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var after = await reader.GetByIdAsync(persistedMessageId, uow.Transaction);

            Assert.Equal(MessageStatus.Sending, after.Status);
            Assert.Equal("worker-1", after.ClaimedBy);
            Assert.NotNull(after.ClaimedAt);
        }
    }

    [Fact]
    public async Task Update_persists_sent_at_and_attempt_count_after_RecordSendSuccess()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreateApprovedMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reader = new MessageReader();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message),
                    uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        // Transition: Approved → Sending → Sent
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetForUpdateAsync(persistedMessageId, uow.Transaction);
            loaded.StartSending("worker-1", DateTimeOffset.UtcNow);
            await messageWriter.UpdateAsync(loaded, uow.Transaction);
            await uow.CommitAsync();
        }

        var sentAt = DateTimeOffset.UtcNow;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetForUpdateAsync(persistedMessageId, uow.Transaction);
            loaded.RecordSendSuccess(sentAt);
            await messageWriter.UpdateAsync(loaded, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var after = await reader.GetByIdAsync(persistedMessageId, uow.Transaction);

            Assert.Equal(MessageStatus.Sent, after.Status);
            Assert.NotNull(after.SentAt);
            Assert.Equal(1, after.AttemptCount);
            Assert.Null(after.FailureReason);
        }
    }

    [Fact]
    public async Task Update_persists_failure_reason_after_RecordSendAttemptFailure()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreateApprovedMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reader = new MessageReader();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message),
                    uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        // Transition: Approved → Sending
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetForUpdateAsync(persistedMessageId, uow.Transaction);
            loaded.StartSending("worker-1", DateTimeOffset.UtcNow);
            await messageWriter.UpdateAsync(loaded, uow.Transaction);
            await uow.CommitAsync();
        }

        // Fail with retries remaining → back to Approved
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var loaded = await reader.GetForUpdateAsync(persistedMessageId, uow.Transaction);
            loaded.RecordSendAttemptFailure(3, "SMTP timeout", DateTimeOffset.UtcNow);
            await messageWriter.UpdateAsync(loaded, uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var after = await reader.GetByIdAsync(persistedMessageId, uow.Transaction);

            Assert.Equal(MessageStatus.Approved, after.Status);
            Assert.Equal("SMTP timeout", after.FailureReason);
            Assert.Equal(1, after.AttemptCount);
        }
    }

    [Fact]
    public async Task Update_for_nonexistent_message_throws_NotFoundException()
    {
        await ResetDbAsync();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        var phantom = TestData.CreateApprovedMessage(Guid.NewGuid());

        await using var uow = await UnitOfWork.BeginAsync(connectionFactory);
        await Assert.ThrowsAsync<NotFoundException>(() => messageWriter.UpdateAsync(phantom, uow.Transaction));
    }
}