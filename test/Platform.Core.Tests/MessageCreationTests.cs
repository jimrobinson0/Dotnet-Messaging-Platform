namespace Messaging.Platform.Core.Tests;

public class MessageCreationTests
{
    [Fact]
    public void Auto_approved_message_starts_in_Approved_with_unpersisted_timestamps()
    {
        var message = Message.CreateApproved(
            id: Guid.NewGuid(),
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Test",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: null);

        Assert.Equal(MessageStatus.Approved, message.Status);
        Assert.Equal(0, message.AttemptCount);
        Assert.Equal(DateTimeOffset.MinValue, message.CreatedAt);
        Assert.Equal(DateTimeOffset.MinValue, message.UpdatedAt);
    }

    [Fact]
    public void Approval_required_message_starts_in_PendingApproval_with_unpersisted_timestamps()
    {
        var message = Message.CreatePendingApproval(
            id: Guid.NewGuid(),
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Test",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: null);

        Assert.Equal(MessageStatus.PendingApproval, message.Status);
        Assert.Equal(DateTimeOffset.MinValue, message.CreatedAt);
        Assert.Equal(DateTimeOffset.MinValue, message.UpdatedAt);
    }

    [Fact]
    public void Idempotency_key_is_trimmed_and_empty_values_become_null()
    {
        var trimmed = Message.CreateApproved(
            id: Guid.NewGuid(),
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Test",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: null,
            idempotencyKey: "  abc-key  ");

        var empty = Message.CreateApproved(
            id: Guid.NewGuid(),
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Test",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: null,
            idempotencyKey: "   ");

        Assert.Equal("abc-key", trimmed.IdempotencyKey);
        Assert.Null(empty.IdempotencyKey);
    }

    [Fact]
    public void Idempotency_key_longer_than_128_characters_throws()
    {
        var tooLong = new string('x', 129);

        Assert.Throws<ArgumentException>(() => Message.CreateApproved(
            id: Guid.NewGuid(),
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Test",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: null,
            idempotencyKey: tooLong));
    }
}
