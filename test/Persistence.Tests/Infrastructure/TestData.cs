using System.Text.Json;
using Messaging.Core;
using Messaging.Core.Audit;

namespace Messaging.Persistence.Tests.Infrastructure;

internal static class TestData
{
    public static Message CreatePendingApprovalMessage(
        Guid messageId,
        Guid? replyToMessageId = null)
    {
        return CreatePendingApprovalMessage(
            messageId,
            $"pending-{Guid.NewGuid():N}",
            replyToMessageId);
    }

    public static Message CreatePendingApprovalMessage(
        Guid messageId,
        string idempotencyKey,
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
        Guid? replyToMessageId = null)
    {
        return CreateApprovedMessage(
            messageId,
            $"approved-{Guid.NewGuid():N}",
            replyToMessageId);
    }

    public static Message CreateApprovedMessage(
        Guid messageId,
        string idempotencyKey,
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
            $"template-{Guid.NewGuid():N}",
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
            $"no-participants-{Guid.NewGuid():N}",
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
                DateTimeOffset.MinValue),

            new(
                Guid.NewGuid(),
                messageId,
                MessageParticipantRole.To,
                "to@example.com",
                "To",
                DateTimeOffset.MinValue)
        };
    }

    public static MessageAuditEvent CreateAuditEvent(
        Guid messageId,
        MessageStatus? fromStatus,
        MessageStatus? toStatus,
        AuditEventType eventType)
    {
        return new MessageAuditEvent(
            Guid.NewGuid(),
            messageId,
            eventType,
            fromStatus,
            toStatus,
            "System",
            "test",
            DateTimeOffset.MinValue);
    }

    public static MessageReview CreateApprovedReview(Guid messageId)
    {
        return new MessageReview(
            Guid.NewGuid(),
            messageId,
            ReviewDecision.Approved,
            "reviewer",
            "ok");
    }

    public static MessageReview CreateRejectedReview(Guid messageId)
    {
        return new MessageReview(
            Guid.NewGuid(),
            messageId,
            ReviewDecision.Rejected,
            "reviewer",
            "no");
    }
}
