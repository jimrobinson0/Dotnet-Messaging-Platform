using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Messages;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Tests.Infrastructure;
using Npgsql;
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
        var message = TestData.CreatePendingApprovalMessage(messageId, "round-trip-key");

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var participantWriter = new ParticipantWriter();
            var auditWriter = new AuditWriter();

            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message), uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            Assert.NotEqual(message.Id, insertResult.MessageId);
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(insertResult.MessageId, ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);

            await auditWriter.InsertAsync(
                TestData.CreateAuditEvent(insertResult.MessageId, null, message.Status, "MessageCreated"),
                uow.Transaction);

            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(insertResult.MessageId, uow.Transaction);

            Assert.Equal(insertResult.MessageId, loaded.Id);
            Assert.Equal("email", loaded.Channel);
            Assert.Equal(message.Status, loaded.Status);
            Assert.Equal(message.ContentSource, loaded.ContentSource);
            Assert.Null(loaded.ClaimedBy);
            Assert.Null(loaded.ClaimedAt);
            Assert.Null(loaded.SentAt);
            Assert.Null(loaded.FailureReason);
            Assert.Equal(0, loaded.AttemptCount);
            Assert.Equal("Subject", loaded.Subject);
            Assert.Equal("Hello", loaded.TextBody);
            Assert.Null(loaded.HtmlBody);
            Assert.Equal("round-trip-key", loaded.IdempotencyKey);

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
        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var participantWriter = new ParticipantWriter();

            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message), uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(insertResult.MessageId, ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(insertResult.MessageId, uow.Transaction);

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
        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            var participantWriter = new ParticipantWriter();

            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message), uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(insertResult.MessageId, ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(insertResult.MessageId, uow.Transaction);

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
        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message), uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            Assert.NotEqual(message.Id, insertResult.MessageId);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(insertResult.MessageId, uow.Transaction);

            Assert.Equal(insertResult.MessageId, loaded.Id);
            Assert.Equal("email", loaded.Channel);
            Assert.Empty(loaded.Participants);
        }
    }

    [Fact]
    public async Task Insert_message_uses_db_generated_id_instead_of_aggregate_id()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();
        var message = TestData.CreatePendingApprovalMessage(messageId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        MessageInsertResult insertResult;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var messageWriter = new MessageWriter();
            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message), uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            Assert.NotEqual(message.Id, insertResult.MessageId);
            await uow.CommitAsync();
        }

        await using var verifyUow = await UnitOfWork.BeginAsync(connectionFactory);
        var reader = new MessageReader();
        var loaded = await reader.GetByIdAsync(insertResult.MessageId, verifyUow.Transaction);
        Assert.Equal(insertResult.MessageId, loaded.Id);
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

    [Fact]
    public async Task InsertIdempotentAsync_same_idempotency_key_returns_same_message_id_and_single_row()
    {
        await ResetDbAsync();

        const string key = "writer-same-key";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstMessage = TestData.CreateApprovedMessage(firstId, key);
        var secondMessage = TestData.CreateApprovedMessage(secondId, key);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        MessageInsertResult firstInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            firstInsert = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(firstMessage), uow.Transaction);
            await uow.CommitAsync();
        }

        MessageInsertResult secondInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            secondInsert = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(secondMessage), uow.Transaction);
            await uow.CommitAsync();
        }

        Assert.True(firstInsert.WasCreated);
        Assert.NotEqual(Guid.Empty, firstInsert.MessageId);
        Assert.NotEqual(firstMessage.Id, firstInsert.MessageId);
        Assert.False(secondInsert.WasCreated);
        Assert.Equal(firstInsert.MessageId, secondInsert.MessageId);
        Assert.NotEqual(secondMessage.Id, secondInsert.MessageId);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>(
            "select count(1) from messages where idempotency_key = @Key",
            new { Key = key });

        Assert.Equal(1, messageCount);
    }

    [Fact]
    public async Task InsertIdempotentAsync_same_key_different_payload_does_not_mutate_original_row()
    {
        await ResetDbAsync();

        const string key = "writer-non-mutation-key";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        var firstMessage = Message.CreateApproved(
            id: firstId,
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "A",
            textBody: "Body-A",
            htmlBody: null,
            templateVariables: (JsonElement?)null,
            idempotencyKey: key,
            participants: Array.Empty<MessageParticipant>());

        var secondMessage = Message.CreateApproved(
            id: secondId,
            channel: "sms",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "B",
            textBody: "Body-B",
            htmlBody: "<p>B</p>",
            templateVariables: (JsonElement?)null,
            idempotencyKey: key,
            participants: Array.Empty<MessageParticipant>());

        MessageInsertResult firstInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            firstInsert = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(firstMessage), uow.Transaction);
            await uow.CommitAsync();
        }

        MessageInsertResult secondInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            secondInsert = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(secondMessage), uow.Transaction);
            await uow.CommitAsync();
        }

        Assert.True(firstInsert.WasCreated);
        Assert.False(secondInsert.WasCreated);
        Assert.Equal(firstInsert.MessageId, secondInsert.MessageId);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var persisted = await connection.QuerySingleAsync<(string Channel, string? Subject, string? TextBody, string? HtmlBody)>(
            """
            select channel as Channel, subject as Subject, text_body as TextBody, html_body as HtmlBody
            from messages
            where id = @MessageId
            """,
            new { MessageId = firstInsert.MessageId });

        Assert.Equal("email", persisted.Channel);
        Assert.Equal("A", persisted.Subject);
        Assert.Equal("Body-A", persisted.TextBody);
        Assert.Null(persisted.HtmlBody);
    }

    [Fact]
    public async Task InsertIdempotentAsync_without_idempotency_key_creates_distinct_rows()
    {
        await ResetDbAsync();

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstMessage = TestData.CreateApprovedMessage(firstId);
        var secondMessage = TestData.CreateApprovedMessage(secondId);

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        MessageInsertResult firstInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            firstInsert = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(firstMessage), uow.Transaction);
            await uow.CommitAsync();
        }

        MessageInsertResult secondInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            secondInsert = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(secondMessage), uow.Transaction);
            await uow.CommitAsync();
        }

        Assert.True(firstInsert.WasCreated);
        Assert.True(secondInsert.WasCreated);
        Assert.NotEqual(firstInsert.MessageId, secondInsert.MessageId);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>(
            "select count(1) from messages where id = any(@Ids)",
            new { Ids = new[] { firstInsert.MessageId, secondInsert.MessageId } });

        Assert.Equal(2, messageCount);
    }

    [Fact]
    public async Task InsertIdempotentAsync_concurrent_same_key_returns_single_message_id_and_single_row()
    {
        await ResetDbAsync();

        const string key = "writer-concurrency-key";
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        var tasks = Enumerable.Range(0, 12)
            .Select(async i =>
            {
                var messageId = Guid.NewGuid();
                var message = Message.CreateApproved(
                    id: messageId,
                    channel: "email",
                    contentSource: MessageContentSource.Direct,
                    templateKey: null,
                    templateVersion: null,
                    templateResolvedAt: null,
                    subject: $"subject-{i}",
                    textBody: "body",
                    htmlBody: null,
                    templateVariables: (JsonElement?)null,
                    idempotencyKey: key,
                    participants: Array.Empty<MessageParticipant>());

                await using var uow = await UnitOfWork.BeginAsync(connectionFactory);
                var result = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message), uow.Transaction);
                await uow.CommitAsync();
                return result;
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var distinctMessageIds = results.Select(result => result.MessageId).Distinct().ToArray();

        Assert.Single(distinctMessageIds);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>(
            "select count(1) from messages where idempotency_key = @Key",
            new { Key = key });

        Assert.Equal(1, messageCount);
    }

    [Fact]
    public async Task CreateAsync_with_same_idempotency_key_returns_same_message_and_inserts_once()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        const string key = "same-key";

        var firstMessageId = Guid.NewGuid();
        var firstMessage = TestData.CreatePendingApprovalMessage(firstMessageId, key);
        var firstResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(firstMessage),
            ParticipantPrototypeMapper.FromCore(firstMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondMessageId = Guid.NewGuid();
        var secondMessage = TestData.CreatePendingApprovalMessage(secondMessageId, key);
        var secondResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(secondMessage),
            ParticipantPrototypeMapper.FromCore(secondMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                secondMessage.Status,
                "MessageCreated"));

        Assert.True(firstResult.WasCreated);
        Assert.False(secondResult.WasCreated);
        Assert.Equal(firstResult.Message.Id, secondResult.Message.Id);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>(
            "select count(1) from messages where idempotency_key = @Key",
            new { Key = key });

        var participantCount = await connection.QuerySingleAsync<int>(
            "select count(1) from message_participants where message_id = @MessageId",
            new { MessageId = firstResult.Message.Id });

        var auditCount = await connection.QuerySingleAsync<int>(
            "select count(1) from message_audit_events where message_id = @MessageId",
            new { MessageId = firstResult.Message.Id });

        Assert.Equal(1, messageCount);
        Assert.Equal(firstMessage.Participants.Count, participantCount);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task CreateAsync_with_same_key_and_different_payload_does_not_mutate_existing_message()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        const string key = "subject-key";

        var firstId = Guid.NewGuid();
        var firstParticipants = TestData.CreateParticipants(firstId);
        var firstMessage = Message.CreateApproved(
            id: firstId,
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "A",
            textBody: "Body-A",
            htmlBody: null,
            templateVariables: (JsonElement?)null,
            idempotencyKey: key,
            participants: firstParticipants);

        var firstResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(firstMessage),
            ParticipantPrototypeMapper.FromCore(firstParticipants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondId = Guid.NewGuid();
        var secondParticipants = TestData.CreateParticipants(secondId);
        var secondMessage = Message.CreateApproved(
            id: secondId,
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "B",
            textBody: "Body-B",
            htmlBody: "<p>B</p>",
            templateVariables: (JsonElement?)null,
            idempotencyKey: key,
            participants: secondParticipants);

        var replayResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(secondMessage),
            ParticipantPrototypeMapper.FromCore(secondParticipants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                secondMessage.Status,
                "MessageCreated"));

        Assert.Equal(firstResult.Message.Id, replayResult.Message.Id);
        Assert.False(replayResult.WasCreated);
        Assert.Equal("A", replayResult.Message.Subject);
        Assert.Equal("Body-A", replayResult.Message.TextBody);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var participantCount = await connection.QuerySingleAsync<int>(
            "select count(1) from message_participants where message_id = @MessageId",
            new { MessageId = firstResult.Message.Id });

        Assert.Equal(firstParticipants.Count, participantCount);
    }

    [Fact]
    public async Task CreateAsync_without_idempotency_key_creates_distinct_messages()
    {
        await ResetDbAsync();

        var repository = CreateRepository();

        var firstId = Guid.NewGuid();
        var firstMessage = TestData.CreatePendingApprovalMessage(firstId);
        var firstResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(firstMessage),
            ParticipantPrototypeMapper.FromCore(firstMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondId = Guid.NewGuid();
        var secondMessage = TestData.CreatePendingApprovalMessage(secondId);
        var secondResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(secondMessage),
            ParticipantPrototypeMapper.FromCore(secondMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                secondMessage.Status,
                "MessageCreated"));

        Assert.True(firstResult.WasCreated);
        Assert.True(secondResult.WasCreated);
        Assert.NotEqual(firstResult.Message.Id, secondResult.Message.Id);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>("select count(1) from messages");
        Assert.Equal(2, messageCount);
    }

    [Fact]
    public async Task CreateAsync_concurrent_requests_with_same_key_store_one_message()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        const string key = "concurrency-key";

        var tasks = Enumerable.Range(0, 12)
            .Select(async i =>
            {
                var messageId = Guid.NewGuid();
                var participants = TestData.CreateParticipants(messageId);
                var message = Message.CreateApproved(
                    id: messageId,
                    channel: "email",
                    contentSource: MessageContentSource.Direct,
                    templateKey: null,
                    templateVersion: null,
                    templateResolvedAt: null,
                    subject: $"subject-{i}",
                    textBody: "body",
                    htmlBody: null,
                    templateVariables: (JsonElement?)null,
                    idempotencyKey: key,
                    participants: participants);

                return await repository.CreateAsync(
                    MessageCreateIntentMapper.ToCreateIntent(message),
                    ParticipantPrototypeMapper.FromCore(participants),
                    persistedMessageId => TestData.CreateAuditEvent(
                        persistedMessageId,
                        null,
                        message.Status,
                        "MessageCreated"));
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var distinctMessageIds = results
            .Select(result => result.Message.Id)
            .Distinct()
            .ToArray();

        Assert.Single(distinctMessageIds);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>(
            "select count(1) from messages where idempotency_key = @Key",
            new { Key = key });

        var participantCount = await connection.QuerySingleAsync<int>(
            "select count(1) from message_participants where message_id = @MessageId",
            new { MessageId = distinctMessageIds[0] });

        var auditCount = await connection.QuerySingleAsync<int>(
            "select count(1) from message_audit_events where message_id = @MessageId",
            new { MessageId = distinctMessageIds[0] });

        Assert.Equal(1, messageCount);
        Assert.Equal(2, participantCount);
        Assert.Equal(1, auditCount);
    }

    private MessageRepository CreateRepository()
    {
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        return new MessageRepository(
            connectionFactory,
            new MessageReader(),
            new MessageWriter(),
            new ParticipantWriter(),
            new Messaging.Platform.Persistence.Reviews.ReviewWriter(),
            new AuditWriter());
    }
}
