using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Messaging.Persistence.Messages.Mapping;
using Messaging.Persistence.Messages.Writes;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Messaging.Persistence.Tests.Infrastructure;

namespace Messaging.Persistence.Tests.Reviews;

public sealed class MessageReviewPersistenceTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_approved_review_persists_correctly()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reviewWriter = new ReviewWriter();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                    uow.Transaction);
            Assert.True(insertResult.Inserted);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        var review = TestData.CreateApprovedReview(persistedMessageId);

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            await reviewWriter.InsertAsync(review, uow.Transaction);

            var row = await uow.Connection.QuerySingleAsync<dynamic>(
                """
                select decision::text as Decision, decided_by as DecidedBy, notes as Notes
                from core.message_reviews
                where message_id = @MessageId
                """,
                new { MessageId = persistedMessageId },
                uow.Transaction);

            Assert.Equal("Approved", (string)row.decision);
            Assert.Equal("reviewer", (string)row.decidedby);
            Assert.Equal("ok", (string)row.notes);

            await uow.CommitAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_rejected_review_persists_correctly()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reviewWriter = new ReviewWriter();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                    uow.Transaction);
            Assert.True(insertResult.Inserted);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        var review = TestData.CreateRejectedReview(persistedMessageId);

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            await reviewWriter.InsertAsync(review, uow.Transaction);

            var row = await uow.Connection.QuerySingleAsync<dynamic>(
                """
                select decision::text as Decision, notes as Notes
                from core.message_reviews
                where message_id = @MessageId
                """,
                new { MessageId = persistedMessageId },
                uow.Transaction);

            Assert.Equal("Rejected", (string)row.decision);
            Assert.Equal("no", (string)row.notes);

            await uow.CommitAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inserting_two_reviews_for_same_message_throws_ConcurrencyException()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reviewWriter = new ReviewWriter();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                    uow.Transaction);
            Assert.True(insertResult.Inserted);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var review1 = TestData.CreateApprovedReview(persistedMessageId);
            await reviewWriter.InsertAsync(review1, uow.Transaction);

            var review2 = TestData.CreateApprovedReview(persistedMessageId);
            await Assert.ThrowsAsync<ConcurrencyException>(() => reviewWriter.InsertAsync(review2, uow.Transaction));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Review_with_null_notes_persists_correctly()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();
        var participantWriter = new ParticipantWriter();
        var reviewWriter = new ReviewWriter();
        Guid persistedMessageId;

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var insertResult =
                await messageWriter.InsertIdempotentAsync(InsertMessageRecordMapper.ToInsertRecord(message, true),
                    uow.Transaction);
            Assert.True(insertResult.Inserted);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            persistedMessageId = insertResult.MessageId;
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(persistedMessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        var review = new MessageReview(
            Guid.NewGuid(),
            persistedMessageId,
            ReviewDecision.Approved,
            "reviewer",
            DateTimeOffset.UtcNow,
            null);

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            await reviewWriter.InsertAsync(review, uow.Transaction);

            var notes = await uow.Connection.QuerySingleAsync<string?>(
                "select notes from core.message_reviews where message_id = @MessageId",
                new { MessageId = persistedMessageId },
                uow.Transaction);

            Assert.Null(notes);

            await uow.CommitAsync();
        }
    }
}