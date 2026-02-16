namespace Messaging.Core.Tests;

public class MessageCreationTests
{
    [Fact]
    public void Auto_approved_message_starts_in_Approved_with_unpersisted_timestamps()
    {
        var message = Message.Create(new MessageCreateSpec(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            null,
            Array.Empty<MessageParticipant>(),
            null));

        Assert.Equal(MessageStatus.Approved, message.Status);
        Assert.Equal(0, message.AttemptCount);
        Assert.Equal(DateTimeOffset.MinValue, message.CreatedAt);
        Assert.Equal(DateTimeOffset.MinValue, message.UpdatedAt);
    }

    [Fact]
    public void Approval_required_message_starts_in_PendingApproval_with_unpersisted_timestamps()
    {
        var message = Message.Create(new MessageCreateSpec(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            true,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            null,
            Array.Empty<MessageParticipant>(),
            null));

        Assert.Equal(MessageStatus.PendingApproval, message.Status);
        Assert.Equal(DateTimeOffset.MinValue, message.CreatedAt);
        Assert.Equal(DateTimeOffset.MinValue, message.UpdatedAt);
    }

    [Fact]
    public void Idempotency_key_is_trimmed_and_empty_values_become_null()
    {
        var trimmed = Message.Create(new MessageCreateSpec(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            "  abc-key  ",
            Array.Empty<MessageParticipant>(),
            null));

        var empty = Message.Create(new MessageCreateSpec(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            "   ",
            Array.Empty<MessageParticipant>(),
            null));

        Assert.Equal("abc-key", trimmed.IdempotencyKey);
        Assert.Null(empty.IdempotencyKey);
    }

    [Fact]
    public void Idempotency_key_longer_than_128_characters_throws()
    {
        var tooLong = new string('x', 129);

        Assert.Throws<ArgumentException>(() => Message.Create(new MessageCreateSpec(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            tooLong,
            Array.Empty<MessageParticipant>(),
            null)));
    }

    [Fact]
    public void Reply_to_message_id_is_preserved_from_create_spec()
    {
        var replyToMessageId = Guid.NewGuid();

        var message = Message.Create(new MessageCreateSpec(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Test",
            "Hello",
            null,
            null,
            null,
            Array.Empty<MessageParticipant>(),
            replyToMessageId));

        Assert.Equal(replyToMessageId, message.ReplyToMessageId);
    }

    [Fact]
    public void Create_throws_when_participants_is_null()
    {
        var spec = new MessageCreateSpec(
            Id: Guid.NewGuid(),
            Channel: "email",
            ContentSource: MessageContentSource.Direct,
            RequiresApproval: false,
            TemplateKey: null,
            TemplateVersion: null,
            TemplateResolvedAt: null,
            Subject: "TestSubject",
            TextBody: "TestBody",
            HtmlBody: null,
            TemplateVariables: null,
            IdempotencyKey: null,
            Participants: null!,
            ReplyToMessageId: null);

        Assert.Throws<ArgumentNullException>(() => Message.Create(spec));
    }
}
