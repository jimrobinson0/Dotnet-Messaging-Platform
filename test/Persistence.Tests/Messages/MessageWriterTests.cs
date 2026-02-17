using Messaging.Persistence.Db;
using Messaging.Persistence.Messages.Reads;
using Messaging.Persistence.Messages.Writes;
using Messaging.Persistence.Tests.Infrastructure;
using Messaging.Core;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageWriterTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task InsertIdempotentAsync_replay_does_not_change_created_or_updated_timestamps()
    {
        await ResetDbAsync();

        const string idempotencyKey = "writer-replay-key";
        var connectionFactory = new DbConnectionFactory(Fixture.ConnectionString);
        var writer = new MessageWriter();
        var reader = new MessageReader();

        Guid firstId;
        await using (var firstUow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var firstMessage = TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey);
            var firstInsert = await writer.InsertIdempotentAsync(
                InsertMessageRecordMapper.ToInsertRecord(firstMessage, false),
                firstUow.Transaction);
            firstId = firstInsert.MessageId;
            await firstUow.CommitAsync();
        }

        Message original;
        await using (var readOriginalUow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            original = await reader.GetByIdAsync(firstId, readOriginalUow.Transaction);
        }

        Guid replayId;
        await using (var replayUow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            var replayMessage = TestData.CreateApprovedMessage(Guid.NewGuid(), idempotencyKey);
            var replayInsert = await writer.InsertIdempotentAsync(
                InsertMessageRecordMapper.ToInsertRecord(replayMessage, false),
                replayUow.Transaction);
            replayId = replayInsert.MessageId;
            await replayUow.CommitAsync();
        }

        Message replay;
        await using (var readReplayUow = await UnitOfWork.BeginAsync(connectionFactory))
        {
            replay = await reader.GetByIdAsync(replayId, readReplayUow.Transaction);
        }

        Assert.Equal(firstId, replayId);
        Assert.Equal(original.CreatedAt, replay.CreatedAt);
        Assert.Equal(original.UpdatedAt, replay.UpdatedAt);
    }
}
