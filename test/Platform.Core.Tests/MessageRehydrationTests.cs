namespace Messaging.Platform.Core.Tests;
public class MessageRehydrationTests
{
    [Fact]
    public void Rehydrated_message_preserves_persistence_timestamps()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedAt = DateTimeOffset.UtcNow;

        var message = new Message(
            id: Guid.NewGuid(),
            channel: "email",
            status: MessageStatus.Approved,
            contentSource: MessageContentSource.Direct,
            createdAt: createdAt,
            updatedAt: updatedAt,
            claimedBy: null,
            claimedAt: null,
            sentAt: null,
            failureReason: null,
            attemptCount: 0,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Test",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: null,
            participants: null);

        Assert.Equal(createdAt, message.CreatedAt);
        Assert.Equal(updatedAt, message.UpdatedAt);
    }
}
