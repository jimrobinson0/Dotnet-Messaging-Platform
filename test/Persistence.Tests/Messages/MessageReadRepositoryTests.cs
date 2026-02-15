using Dapper;
using Messaging.Core;
using Messaging.Persistence.Db;
using Messaging.Persistence.Messages;
using Messaging.Persistence.Messages.Reads;
using Messaging.Persistence.Tests.Infrastructure;
using Npgsql;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageReadRepositoryTests : PostgresTestBase
{
    public MessageReadRepositoryTests(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_respects_page_and_page_size()
    {
        await ResetDbAsync();

        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = Enumerable.Range(0, 5)
            .Select(i => new MessageSeed(
                Guid.NewGuid(),
                "email",
                "Approved",
                false,
                $"msg-{i}",
                createdAt.AddMinutes(i),
                null,
                null))
            .ToArray();

        await SeedMessagesAsync(rows);

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            2,
            2,
            [MessageStatus.Approved],
            null,
            null,
            null,
            null,
            null,
            null));

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var ordered = rows
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToArray();

        Assert.Equal(ordered[2].Id, result.Items.ElementAt(0).Id);
        Assert.Equal(ordered[3].Id, result.Items.ElementAt(1).Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_filters_by_single_status()
    {
        await ResetDbAsync();

        var now = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        await SeedMessagesAsync(
            new MessageSeed(Guid.NewGuid(), "email", "Approved", false, "approved", now, null, null),
            new MessageSeed(Guid.NewGuid(), "email", "Failed", false, "failed-1", now.AddMinutes(1), now.AddMinutes(2),
                "boom"),
            new MessageSeed(Guid.NewGuid(), "sms", "Failed", false, "failed-2", now.AddMinutes(3), now.AddMinutes(4),
                "boom2"));

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            50,
            [MessageStatus.Failed],
            null,
            null,
            null,
            null,
            null,
            null));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(MessageStatus.Failed, item.Status));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_filters_by_multiple_statuses()
    {
        await ResetDbAsync();

        var now = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        await SeedMessagesAsync(
            new MessageSeed(Guid.NewGuid(), "email", "Approved", false, "approved", now, null, null),
            new MessageSeed(Guid.NewGuid(), "email", "Failed", false, "failed", now.AddMinutes(1), now.AddMinutes(1),
                "boom"),
            new MessageSeed(Guid.NewGuid(), "email", "Sent", false, "sent", now.AddMinutes(2), now.AddMinutes(2), null));

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            50,
            [MessageStatus.Approved, MessageStatus.Failed],
            null,
            null,
            null,
            null,
            null,
            null));

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, item => item.Status == MessageStatus.Sent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_filters_by_created_range_with_inclusive_from_and_exclusive_to()
    {
        await ResetDbAsync();

        var baseTime = new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero);
        var inside = Guid.NewGuid();

        await SeedMessagesAsync(
            new MessageSeed(Guid.NewGuid(), "email", "Approved", false, "before", baseTime, null, null),
            new MessageSeed(inside, "email", "Approved", false, "inside", baseTime.AddHours(1), null, null),
            new MessageSeed(Guid.NewGuid(), "email", "Approved", false, "at-upper", baseTime.AddHours(2), null, null));

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            50,
            [MessageStatus.Approved],
            null,
            baseTime.AddHours(1),
            baseTime.AddHours(2),
            null,
            null,
            null));

        Assert.Single(result.Items);
        Assert.Equal(inside, result.Items.Single().Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_orders_by_created_at_desc_then_id_desc_deterministically()
    {
        await ResetDbAsync();

        var tiedCreatedAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var newest = Guid.NewGuid();

        await SeedMessagesAsync(
            new MessageSeed(lowerId, "email", "Approved", false, "lower", tiedCreatedAt, null, null),
            new MessageSeed(higherId, "email", "Approved", false, "higher", tiedCreatedAt, null, null),
            new MessageSeed(newest, "email", "Approved", false, "newest", tiedCreatedAt.AddMinutes(1), null, null));

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            50,
            [MessageStatus.Approved],
            null,
            null,
            null,
            null,
            null,
            null));

        var orderedIds = result.Items.Select(item => item.Id).ToArray();
        Assert.Equal(newest, orderedIds[0]);
        Assert.Equal(higherId, orderedIds[1]);
        Assert.Equal(lowerId, orderedIds[2]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_returns_total_count_for_full_filtered_result()
    {
        await ResetDbAsync();

        var now = new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero);
        await SeedMessagesAsync(
            new MessageSeed(Guid.NewGuid(), "email", "Failed", false, "failed-1", now, now, "boom"),
            new MessageSeed(Guid.NewGuid(), "email", "Failed", false, "failed-2", now.AddMinutes(1), now, "boom"),
            new MessageSeed(Guid.NewGuid(), "email", "Failed", false, "failed-3", now.AddMinutes(2), now, "boom"),
            new MessageSeed(Guid.NewGuid(), "email", "Sent", false, "sent", now.AddMinutes(3), now, null));

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            2,
            [MessageStatus.Failed],
            null,
            null,
            null,
            null,
            null,
            null));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_returns_empty_items_when_no_rows_match()
    {
        await ResetDbAsync();

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            50,
            [MessageStatus.Rejected],
            null,
            null,
            null,
            null,
            null,
            null));

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListAsync_filters_by_channel_sent_range_and_requires_approval()
    {
        await ResetDbAsync();

        var baseTime = new DateTimeOffset(2026, 1, 7, 0, 0, 0, TimeSpan.Zero);
        var matchId = Guid.NewGuid();

        await SeedMessagesAsync(
            new MessageSeed(matchId, "email", "Failed", true, "match", baseTime, baseTime.AddHours(1), "boom"),
            new MessageSeed(Guid.NewGuid(), "sms", "Failed", true, "wrong-channel", baseTime, baseTime.AddHours(1),
                "boom"),
            new MessageSeed(Guid.NewGuid(), "email", "Failed", false, "wrong-approval", baseTime,
                baseTime.AddHours(1), "boom"),
            new MessageSeed(Guid.NewGuid(), "email", "Failed", true, "outside-sent-window", baseTime,
                baseTime.AddHours(4), "boom"));

        var repository = CreateRepository();
        var result = await repository.ListAsync(new MessageReadQuery(
            1,
            50,
            [MessageStatus.Failed],
            "email",
            null,
            null,
            baseTime.AddHours(1),
            baseTime.AddHours(2),
            true));

        Assert.Single(result.Items);
        Assert.Equal(matchId, result.Items.Single().Id);
    }

    private MessageReadRepository CreateRepository()
    {
        return new MessageReadRepository(new DbConnectionFactory(Fixture.ConnectionString));
    }

    private async Task SeedMessagesAsync(params MessageSeed[] rows)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
                           insert into core.messages (
                             id,
                             channel,
                             status,
                             requires_approval,
                             content_source,
                             subject,
                             text_body,
                             created_at,
                             updated_at,
                             sent_at,
                             failure_reason
                           )
                           values (
                             @Id,
                             @Channel,
                             @Status::core.message_status,
                             @RequiresApproval,
                             'Direct',
                             @Subject,
                             'body',
                             @CreatedAt,
                             @CreatedAt,
                             @SentAt,
                             @FailureReason
                           );
                           """;

        await connection.ExecuteAsync(sql, rows);
    }

    private sealed record MessageSeed(
        Guid Id,
        string Channel,
        string Status,
        bool RequiresApproval,
        string Subject,
        DateTimeOffset CreatedAt,
        DateTimeOffset? SentAt,
        string? FailureReason);
}
