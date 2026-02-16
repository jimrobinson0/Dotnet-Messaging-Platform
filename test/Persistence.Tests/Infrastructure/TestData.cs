using System.Text.Json;
using Messaging.Core;
using Messaging.Core.Audit;

namespace Messaging.Persistence.Tests.Infrastructure;

internal static class TestData
{
    public static Message CreatePendingApprovalMessage(
        Guid messageId,
        string? idempotencyKey = null,
        Guid? replyToMessageId = null)
    {
        return Message.Create(new MessageCreateSpec(
            messageId,
            "email",
            MessageContentSource.Direct,
            true,
            null,
            null,
            null,
            "Subject",
            "Hello",
            null,
            null,
            idempotencyKey,
            CreateParticipants(messageId),
            replyToMessageId));
    }

    public static Message CreateApprovedMessage(
        Guid messageId,
        string? idempotencyKey = null,
        Guid? replyToMessageId = null)
    {
        return Message.Create(new MessageCreateSpec(
            messageId,
            "email",
            MessageContentSource.Direct,
            false,
            null,
            null,
            null,
            "Auto-approved subject",
            "Auto-approved body",
            "<p>Auto-approved</p>",
            null,
            idempotencyKey,
            CreateParticipants(messageId),
            replyToMessageId));
    }

    public static Message CreateTemplateMessage(Guid messageId)
    {
        var variablesJson = JsonDocument.Parse("""{"name":"Test","orderId":42}""").RootElement;

        return Message.Create(new MessageCreateSpec(
            messageId,
            "email",
            MessageContentSource.Template,
            true,
            "welcome-email",
            "2.1",
            DateTimeOffset.UtcNow,
            "Welcome",
            "Hello {{name}}",
            "<p>Hello {{name}}</p>",
            variablesJson,
            null,
            CreateParticipants(messageId),
            null));
    }

    public static Message CreateMessageWithoutParticipants(Guid messageId)
    {
        return Message.Create(new MessageCreateSpec(
            messageId,
            "email",
            MessageContentSource.Direct,
            true,
            null,
            null,
            null,
            null,
            "Short message",
            null,
            null,
            null,
            Array.Empty<MessageParticipant>(),
            null));
    }

    public static IReadOnlyList<MessageParticipant> CreateParticipants(Guid messageId)
    {
        return new List<MessageParticipant>
        {
            new(
                Guid.NewGuid(),
                messageId,
                MessageParticipantRole.Sender,
                "sender@example.com",
                "Sender",
                DateTimeOffset.UtcNow),

            new(
                Guid.NewGuid(),
                messageId,
                MessageParticipantRole.To,
                "to@example.com",
                "To",
                DateTimeOffset.UtcNow)
        };
    }

    public static MessageAuditEvent CreateAuditEvent(
        Guid messageId,
        MessageStatus? fromStatus,
        MessageStatus? toStatus,
        AuditEventType eventType,
        JsonElement? metadata = null)
    {
        return new MessageAuditEvent(
            Guid.NewGuid(),
            messageId,
            eventType,
            fromStatus,
            toStatus,
            "System",
            "test",
            DateTimeOffset.UtcNow,
            metadata);
    }

    public static MessageReview CreateApprovedReview(Guid messageId)
    {
        return new MessageReview(
            Guid.NewGuid(),
            messageId,
            ReviewDecision.Approved,
            "reviewer",
            DateTimeOffset.UtcNow,
            "ok");
    }

    public static MessageReview CreateRejectedReview(Guid messageId)
    {
        return new MessageReview(
            Guid.NewGuid(),
            messageId,
            ReviewDecision.Rejected,
            "reviewer",
            DateTimeOffset.UtcNow,
            "no");
    }
}
