namespace Messaging.Core.Tests;

public class MessageRehydrationTests
{
    [Fact]
    public void Rehydrated_message_preserves_persistence_timestamps()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedAt = DateTimeOffset.UtcNow;

        var message = new Message(
            Guid.NewGuid(),
            "email",
            MessageStatus.Approved,
            MessageContentSource.Direct,
            createdAt,
            updatedAt,
            null,
            null,
            null,
            null,
            0,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            "rehydration-key",
            null,
            null,
            null,
            null,
            null);

        Assert.Equal(createdAt, message.CreatedAt);
        Assert.Equal(updatedAt, message.UpdatedAt);
        Assert.Equal("rehydration-key", message.IdempotencyKey);
    }
}
