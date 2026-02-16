using Dapper;
using Messaging.Core;
using Messaging.Core.Exceptions;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Exceptions;
using Messaging.Persistence.Messages;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Messaging.Persistence.Tests.Infrastructure;
using Npgsql;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageInsertTests : PostgresTestBase
{
    public MessageInsertTests(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Category", "Integration")]
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

            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message, true),
                uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            Assert.NotEqual(message.Id, insertResult.MessageId);
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(insertResult.MessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
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
            Assert.Equal(1, loaded.Participants.Count(p => p.Role == MessageParticipantRole.Sender));
            Assert.Equal(1, loaded.Participants.Count(p => p.Role == MessageParticipantRole.To));

            // DB owns timestamps; we just assert they are populated.
            Assert.NotEqual(default, loaded.CreatedAt);
            Assert.NotEqual(default, loaded.UpdatedAt);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
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

            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message, false),
                uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(insertResult.MessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(insertResult.MessageId, uow.Transaction);

            Assert.Equal(MessageStatus.Approved, loaded.Status);
            Assert.Equal("Auto-approved subject", loaded.Subject);
            Assert.Equal("Auto-approved body", loaded.TextBody);
            Assert.Equal("<p>Auto-approved</p>", loaded.HtmlBody);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
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

            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message, true),
                uow.Transaction);
            Assert.True(insertResult.WasCreated);
            Assert.NotEqual(Guid.Empty, insertResult.MessageId);
            await participantWriter.InsertAsync(
                ParticipantPrototypeMapper.Bind(insertResult.MessageId,
                    ParticipantPrototypeMapper.FromCore(message.Participants)),
                uow.Transaction);
            await uow.CommitAsync();
        }

        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var reader = new MessageReader();
            var loaded = await reader.GetByIdAsync(insertResult.MessageId, uow.Transaction);

            Assert.Equal(MessageContentSource.Template, loaded.ContentSource);
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
    [Trait("Category", "Integration")]
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
            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message, true),
                uow.Transaction);
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
    [Trait("Category", "Integration")]
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
            insertResult = await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message, true),
                uow.Transaction);
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
    [Trait("Category", "Integration")]
    public async Task GetByIdAsync_for_nonexistent_id_throws_NotFoundException()
    {
        await ResetDbAsync();

        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        await using var uow = await UnitOfWork.BeginAsync(connectionFactory);

        var reader = new MessageReader();
        await Assert.ThrowsAsync<NotFoundException>(() => reader.GetByIdAsync(Guid.NewGuid(), uow.Transaction));
    }

    [Fact]
    [Trait("Category", "Integration")]
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
            firstInsert =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(firstMessage, false),
                    uow.Transaction);
            await uow.CommitAsync();
        }

        MessageInsertResult secondInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            secondInsert =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(secondMessage, false),
                    uow.Transaction);
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
            "select count(1) from core.messages where idempotency_key = @Key",
            new { Key = key });

        Assert.Equal(1, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InsertIdempotentAsync_same_key_different_payload_does_not_mutate_original_row()
    {
        await ResetDbAsync();

        const string key = "writer-non-mutation-key";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var messageWriter = new MessageWriter();

        var firstMessage = Message.Create(new MessageCreateSpec(
            firstId,
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "A",
            "Body-A",
            null,
            null,
            key,
            Array.Empty<MessageParticipant>(),
            null));

        var secondMessage = Message.Create(new MessageCreateSpec(
            secondId,
            "sms",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "B",
            "Body-B",
            "<p>B</p>",
            null,
            key,
            Array.Empty<MessageParticipant>(),
            null));

        MessageInsertResult firstInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            firstInsert =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(firstMessage, false),
                    uow.Transaction);
            await uow.CommitAsync();
        }

        MessageInsertResult secondInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            secondInsert =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(secondMessage, false),
                    uow.Transaction);
            await uow.CommitAsync();
        }

        Assert.True(firstInsert.WasCreated);
        Assert.False(secondInsert.WasCreated);
        Assert.Equal(firstInsert.MessageId, secondInsert.MessageId);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var persisted =
            await connection.QuerySingleAsync<(string Channel, string? Subject, string? TextBody, string? HtmlBody)>(
                """
                select channel as Channel, subject as Subject, text_body as TextBody, html_body as HtmlBody
                from core.messages
                where id = @MessageId
                """,
                new { firstInsert.MessageId });

        Assert.Equal("email", persisted.Channel);
        Assert.Equal("A", persisted.Subject);
        Assert.Equal("Body-A", persisted.TextBody);
        Assert.Null(persisted.HtmlBody);
    }

    [Fact]
    [Trait("Category", "Integration")]
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
            firstInsert =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(firstMessage, false),
                    uow.Transaction);
            await uow.CommitAsync();
        }

        MessageInsertResult secondInsert;
        await using (var uow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            secondInsert =
                await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(secondMessage, false),
                    uow.Transaction);
            await uow.CommitAsync();
        }

        Assert.True(firstInsert.WasCreated);
        Assert.True(secondInsert.WasCreated);
        Assert.NotEqual(firstInsert.MessageId, secondInsert.MessageId);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var messageCount = await connection.QuerySingleAsync<int>(
            "select count(1) from core.messages where id = any(@Ids)",
            new { Ids = new[] { firstInsert.MessageId, secondInsert.MessageId } });

        Assert.Equal(2, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
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
                var message = Message.Create(new MessageCreateSpec(
                    messageId,
                    "email",
                    MessageContentSource.Direct,
                    false,
                    null,
                    null,
                    null,
                    $"subject-{i}",
                    "body",
                    null,
                    null,
                    key,
                    Array.Empty<MessageParticipant>(),
                    null));

                await using var uow = await UnitOfWork.BeginAsync(connectionFactory);
                var result =
                    await messageWriter.InsertIdempotentAsync(MessageCreateIntentMapper.ToCreateIntent(message, false),
                        uow.Transaction);
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
            "select count(1) from core.messages where idempotency_key = @Key",
            new { Key = key });

        Assert.Equal(1, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_same_idempotency_key_returns_same_message_and_inserts_once()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        const string key = "same-key";

        var firstMessageId = Guid.NewGuid();
        var firstMessage = TestData.CreatePendingApprovalMessage(firstMessageId, key);
        var firstResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(firstMessage, true),
            ParticipantPrototypeMapper.FromCore(firstMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondMessageId = Guid.NewGuid();
        var secondMessage = TestData.CreatePendingApprovalMessage(secondMessageId, key);
        var secondResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(secondMessage, true),
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
            "select count(1) from core.messages where idempotency_key = @Key",
            new { Key = key });

        var participantCount = await connection.QuerySingleAsync<int>(
            "select count(1) from core.message_participants where message_id = @MessageId",
            new { MessageId = firstResult.Message.Id });

        var auditCount = await connection.QuerySingleAsync<int>(
            "select count(1) from core.message_audit_events where message_id = @MessageId",
            new { MessageId = firstResult.Message.Id });

        Assert.Equal(1, messageCount);
        Assert.Equal(firstMessage.Participants.Count, participantCount);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_same_key_and_different_payload_does_not_mutate_existing_message()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        const string key = "subject-key";

        var firstId = Guid.NewGuid();
        var firstParticipants = TestData.CreateParticipants(firstId);
        var firstMessage = Message.Create(new MessageCreateSpec(
            firstId,
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "A",
            "Body-A",
            null,
            null,
            key,
            firstParticipants,
            null));

        var firstResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(firstMessage, false),
            ParticipantPrototypeMapper.FromCore(firstParticipants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondId = Guid.NewGuid();
        var secondParticipants = TestData.CreateParticipants(secondId);
        var secondMessage = Message.Create(new MessageCreateSpec(
            secondId,
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "B",
            "Body-B",
            "<p>B</p>",
            null,
            key,
            secondParticipants,
            null));

        var replayResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(secondMessage, false),
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
            "select count(1) from core.message_participants where message_id = @MessageId",
            new { MessageId = firstResult.Message.Id });

        Assert.Equal(firstParticipants.Count, participantCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_without_idempotency_key_creates_distinct_messages()
    {
        await ResetDbAsync();

        var repository = CreateRepository();

        var firstId = Guid.NewGuid();
        var firstMessage = TestData.CreatePendingApprovalMessage(firstId);
        var firstResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(firstMessage, true),
            ParticipantPrototypeMapper.FromCore(firstMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondId = Guid.NewGuid();
        var secondMessage = TestData.CreatePendingApprovalMessage(secondId);
        var secondResult = await repository.CreateAsync(
            MessageCreateIntentMapper.ToCreateIntent(secondMessage, true),
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

        var messageCount = await connection.QuerySingleAsync<int>("select count(1) from core.messages");
        Assert.Equal(2, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
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
                var message = Message.Create(new MessageCreateSpec(
                    messageId,
                    "email",
                    MessageContentSource.Direct,
                    false,
                    null,
                    null,
                    null,
                    $"subject-{i}",
                    "body",
                    null,
                    null,
                    key,
                    participants,
                    null));

                return await repository.CreateAsync(
                    MessageCreateIntentMapper.ToCreateIntent(message, false),
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
            "select count(1) from core.messages where idempotency_key = @Key",
            new { Key = key });

        var participantCount = await connection.QuerySingleAsync<int>(
            "select count(1) from core.message_participants where message_id = @MessageId",
            new { MessageId = distinctMessageIds[0] });

        var auditCount = await connection.QuerySingleAsync<int>(
            "select count(1) from core.message_audit_events where message_id = @MessageId",
            new { MessageId = distinctMessageIds[0] });

        Assert.Equal(1, messageCount);
        Assert.Equal(2, participantCount);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_reply_target_and_no_existing_references_persists_thread_headers()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var replyTargetId = Guid.NewGuid();
        const string smtpMessageId = "<original-no-refs@example.test>";

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, smtp_message_id, references_header
                )
                values (
                  @Id, 'email', 'Sent'::core.message_status, 'Direct', 'Original', 'Body', @SmtpMessageId, null
                );
                """,
                new
                {
                    Id = replyTargetId,
                    SmtpMessageId = smtpMessageId
                });
        }

        var message = TestData.CreateApprovedMessage(Guid.NewGuid(), replyToMessageId: replyTargetId);
        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message, false);

        var result = await repository.CreateAsync(
            createIntent,
            ParticipantPrototypeMapper.FromCore(message.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                message.Status,
                "MessageCreated"));

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();

        var persisted = await verifyConnection.QuerySingleAsync<(Guid ReplyToMessageId, string InReplyTo, string ReferencesHeader)>(
            """
            select
              reply_to_message_id as ReplyToMessageId,
              in_reply_to as InReplyTo,
              references_header as ReferencesHeader
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = result.Message.Id });

        Assert.Equal(replyTargetId, persisted.ReplyToMessageId);
        Assert.Equal(smtpMessageId, persisted.InReplyTo);
        Assert.Equal(smtpMessageId, persisted.ReferencesHeader);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_reply_target_and_existing_references_appends_smtp_message_id()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var replyTargetId = Guid.NewGuid();
        var rootMessageId = Guid.NewGuid();
        const string smtpMessageId = "<original-with-refs@example.test>";
        const string originalReferences = "<root@example.test> <parent@example.test>";
        const string rootSmtpMessageId = "<root@example.test>";

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, smtp_message_id
                )
                values (
                  @RootId, 'email', 'Sent'::core.message_status, 'Direct', 'Root', 'Body', @RootSmtpMessageId
                );

                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, reply_to_message_id,
                  in_reply_to, smtp_message_id, references_header
                )
                values (
                  @Id, 'email', 'Sent'::core.message_status, 'Direct', 'Original', 'Body', @RootId,
                  @RootSmtpMessageId, @SmtpMessageId, @ReferencesHeader
                );
                """,
                new
                {
                    Id = replyTargetId,
                    RootId = rootMessageId,
                    RootSmtpMessageId = rootSmtpMessageId,
                    SmtpMessageId = smtpMessageId,
                    ReferencesHeader = originalReferences
                });
        }

        var message = TestData.CreateApprovedMessage(Guid.NewGuid(), replyToMessageId: replyTargetId);
        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message, false);

        var result = await repository.CreateAsync(
            createIntent,
            ParticipantPrototypeMapper.FromCore(message.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                message.Status,
                "MessageCreated"));

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();

        var persisted = await verifyConnection.QuerySingleAsync<(string InReplyTo, string ReferencesHeader)>(
            """
            select
              in_reply_to as InReplyTo,
              references_header as ReferencesHeader
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = result.Message.Id });

        Assert.Equal(smtpMessageId, persisted.InReplyTo);
        Assert.Equal($"{originalReferences} {smtpMessageId}", persisted.ReferencesHeader);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_missing_reply_target_throws_invalid_reply_target_and_does_not_insert_message()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var replyTargetId = Guid.NewGuid();
        var message = TestData.CreateApprovedMessage(Guid.NewGuid(), replyToMessageId: replyTargetId);
        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message, false);

        var exception = await Assert.ThrowsAsync<MessageValidationException>(() => repository.CreateAsync(
            createIntent,
            ParticipantPrototypeMapper.FromCore(message.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                message.Status,
                "MessageCreated")));

        Assert.Equal("INVALID_REPLY_TARGET", exception.Code);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var messageCount = await verifyConnection.QuerySingleAsync<int>("select count(1) from core.messages");
        Assert.Equal(0, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_reply_target_not_in_sent_status_throws_invalid_reply_target()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var replyTargetId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, smtp_message_id
                )
                values (
                  @Id, 'email', 'Approved'::core.message_status, 'Direct', 'Original', 'Body', '<approved@example.test>'
                );
                """,
                new { Id = replyTargetId });
        }

        var message = TestData.CreateApprovedMessage(Guid.NewGuid(), replyToMessageId: replyTargetId);
        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message, false);

        var exception = await Assert.ThrowsAsync<MessageValidationException>(() => repository.CreateAsync(
            createIntent,
            ParticipantPrototypeMapper.FromCore(message.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                message.Status,
                "MessageCreated")));

        Assert.Equal("INVALID_REPLY_TARGET", exception.Code);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var messageCount = await verifyConnection.QuerySingleAsync<int>("select count(1) from core.messages");
        Assert.Equal(1, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_reply_target_missing_smtp_message_id_throws_invalid_reply_target()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var replyTargetId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (
                  id, channel, status, content_source, subject, text_body, smtp_message_id
                )
                values (
                  @Id, 'email', 'Sent'::core.message_status, 'Direct', 'Original', 'Body', null
                );
                """,
                new { Id = replyTargetId });
        }

        var message = TestData.CreateApprovedMessage(Guid.NewGuid(), replyToMessageId: replyTargetId);
        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message, false);

        var exception = await Assert.ThrowsAsync<MessageValidationException>(() => repository.CreateAsync(
            createIntent,
            ParticipantPrototypeMapper.FromCore(message.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                message.Status,
                "MessageCreated")));

        Assert.Equal("INVALID_REPLY_TARGET", exception.Code);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var messageCount = await verifyConnection.QuerySingleAsync<int>("select count(1) from core.messages");
        Assert.Equal(1, messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Constraint_rejects_reply_row_when_references_header_is_null()
    {
        await ResetDbAsync();

        var rootMessageId = Guid.NewGuid();
        const string rootSmtpMessageId = "<root-for-constraint@example.test>";

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            """
            insert into core.messages (
              id, channel, status, content_source, subject, text_body, smtp_message_id
            )
            values (
              @RootMessageId, 'email', 'Sent'::core.message_status, 'Direct', 'Root', 'Body', @RootSmtpMessageId
            );
            """,
            new
            {
                RootMessageId = rootMessageId,
                RootSmtpMessageId = rootSmtpMessageId
            });

        var exception = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            """
            insert into core.messages (
              id, channel, status, content_source, subject, text_body,
              reply_to_message_id, in_reply_to, references_header
            )
            values (
              @ReplyMessageId, 'email', 'Approved'::core.message_status, 'Direct', 'Reply', 'Body',
              @RootMessageId, @RootSmtpMessageId, null
            );
            """,
            new
            {
                ReplyMessageId = Guid.NewGuid(),
                RootMessageId = rootMessageId,
                RootSmtpMessageId = rootSmtpMessageId
            }));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Constraint_allows_root_row_with_all_reply_fields_null()
    {
        await ResetDbAsync();

        var messageId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            insert into core.messages (
              id, channel, status, content_source, subject, text_body,
              reply_to_message_id, in_reply_to, references_header
            )
            values (
              @MessageId, 'email', 'Approved'::core.message_status, 'Direct', 'Root', 'Body',
              null, null, null
            );
            """,
            new { MessageId = messageId });

        var persisted = await connection.QuerySingleAsync<(Guid Id, Guid? ReplyToMessageId, string? InReplyTo, string? ReferencesHeader)>(
            """
            select
              id as Id,
              reply_to_message_id as ReplyToMessageId,
              in_reply_to as InReplyTo,
              references_header as ReferencesHeader
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = messageId });

        Assert.Equal(messageId, persisted.Id);
        Assert.Null(persisted.ReplyToMessageId);
        Assert.Null(persisted.InReplyTo);
        Assert.Null(persisted.ReferencesHeader);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_with_same_idempotency_key_does_not_mutate_reply_fields()
    {
        await ResetDbAsync();

        const string idempotencyKey = "reply-immutability-key";
        var repository = CreateRepository();
        var replyTargetAId = Guid.NewGuid();
        var replyTargetBId = Guid.NewGuid();
        const string smtpMessageIdA = "<reply-target-a@example.test>";
        const string smtpMessageIdB = "<reply-target-b@example.test>";

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (id, channel, status, content_source, subject, text_body, smtp_message_id)
                values (@ReplyTargetAId, 'email', 'Sent'::core.message_status, 'Direct', 'Target A', 'Body', @SmtpMessageIdA);

                insert into core.messages (id, channel, status, content_source, subject, text_body, smtp_message_id)
                values (@ReplyTargetBId, 'email', 'Sent'::core.message_status, 'Direct', 'Target B', 'Body', @SmtpMessageIdB);
                """,
                new
                {
                    ReplyTargetAId = replyTargetAId,
                    ReplyTargetBId = replyTargetBId,
                    SmtpMessageIdA = smtpMessageIdA,
                    SmtpMessageIdB = smtpMessageIdB
                });
        }

        var firstMessage =
            TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey, replyTargetAId);
        var firstIntent = MessageCreateIntentMapper.ToCreateIntent(firstMessage, false);

        var firstResult = await repository.CreateAsync(
            firstIntent,
            ParticipantPrototypeMapper.FromCore(firstMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var replayMessageId = Guid.NewGuid();
        var replayMessage = Message.Create(new MessageCreateSpec(
            replayMessageId,
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Mutated Subject",
            "Mutated Body",
            "<p>mutated</p>",
            null,
            idempotencyKey,
            TestData.CreateParticipants(replayMessageId),
            replyTargetBId));

        var replayIntent = MessageCreateIntentMapper.ToCreateIntent(replayMessage, false);

        var replayResult = await repository.CreateAsync(
            replayIntent,
            ParticipantPrototypeMapper.FromCore(replayMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                replayMessage.Status,
                "MessageCreated"));

        Assert.True(firstResult.WasCreated);
        Assert.False(replayResult.WasCreated);
        Assert.Equal(firstResult.Message.Id, replayResult.Message.Id);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var persisted = await verifyConnection.QuerySingleAsync<(Guid ReplyToMessageId, string InReplyTo, string ReferencesHeader)>(
            """
            select
              reply_to_message_id as ReplyToMessageId,
              in_reply_to as InReplyTo,
              references_header as ReferencesHeader
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = firstResult.Message.Id });

        Assert.Equal(replyTargetAId, persisted.ReplyToMessageId);
        Assert.Equal(smtpMessageIdA, persisted.InReplyTo);
        Assert.Equal(smtpMessageIdA, persisted.ReferencesHeader);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAsync_same_idempotency_key_with_different_reply_to_does_not_change_existing_message()
    {
        await ResetDbAsync();

        const string idempotencyKey = "reply-conflict-key";
        var repository = CreateRepository();
        var replyTargetAId = Guid.NewGuid();
        var replyTargetBId = Guid.NewGuid();
        const string smtpMessageIdA = "<reply-target-a-2@example.test>";
        const string smtpMessageIdB = "<reply-target-b-2@example.test>";

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                insert into core.messages (id, channel, status, content_source, subject, text_body, smtp_message_id)
                values (@ReplyTargetAId, 'email', 'Sent'::core.message_status, 'Direct', 'Target A', 'Body', @SmtpMessageIdA);

                insert into core.messages (id, channel, status, content_source, subject, text_body, smtp_message_id)
                values (@ReplyTargetBId, 'email', 'Sent'::core.message_status, 'Direct', 'Target B', 'Body', @SmtpMessageIdB);
                """,
                new
                {
                    ReplyTargetAId = replyTargetAId,
                    ReplyTargetBId = replyTargetBId,
                    SmtpMessageIdA = smtpMessageIdA,
                    SmtpMessageIdB = smtpMessageIdB
                });
        }

        var firstMessage =
            TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey, replyTargetAId);
        var firstIntent = MessageCreateIntentMapper.ToCreateIntent(firstMessage, false);

        var firstResult = await repository.CreateAsync(
            firstIntent,
            ParticipantPrototypeMapper.FromCore(firstMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                firstMessage.Status,
                "MessageCreated"));

        var secondMessage =
            TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey, replyTargetBId);
        var secondIntent = MessageCreateIntentMapper.ToCreateIntent(secondMessage, false);

        var replayResult = await repository.CreateAsync(
            secondIntent,
            ParticipantPrototypeMapper.FromCore(secondMessage.Participants),
            persistedMessageId => TestData.CreateAuditEvent(
                persistedMessageId,
                null,
                secondMessage.Status,
                "MessageCreated"));

        Assert.True(firstResult.WasCreated);
        Assert.False(replayResult.WasCreated);
        Assert.Equal(firstResult.Message.Id, replayResult.Message.Id);

        await using var verifyConnection = new NpgsqlConnection(Fixture.ConnectionString);
        await verifyConnection.OpenAsync();
        var persisted = await verifyConnection.QuerySingleAsync<(Guid ReplyToMessageId, string InReplyTo, string ReferencesHeader)>(
            """
            select
              reply_to_message_id as ReplyToMessageId,
              in_reply_to as InReplyTo,
              references_header as ReferencesHeader
            from core.messages
            where id = @MessageId
            """,
            new { MessageId = firstResult.Message.Id });

        Assert.Equal(replyTargetAId, persisted.ReplyToMessageId);
        Assert.Equal(smtpMessageIdA, persisted.InReplyTo);
        Assert.Equal(smtpMessageIdA, persisted.ReferencesHeader);
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
