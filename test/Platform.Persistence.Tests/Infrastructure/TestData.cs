using System;
using System.Collections.Generic;
using System.Text.Json;
using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Tests.Infrastructure;

internal static class TestData
{
    public static Message CreatePendingApprovalMessage(Guid messageId, string? idempotencyKey = null)
    {
        return Message.CreatePendingApproval(
            id: messageId,
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Subject",
            textBody: "Hello",
            htmlBody: null,
            templateVariables: (JsonElement?)null,
            idempotencyKey: idempotencyKey,
            participants: CreateParticipants(messageId));
    }

    public static Message CreateApprovedMessage(Guid messageId, string? idempotencyKey = null)
    {
        return Message.CreateApproved(
            id: messageId,
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Auto-approved subject",
            textBody: "Auto-approved body",
            htmlBody: "<p>Auto-approved</p>",
            templateVariables: (JsonElement?)null,
            idempotencyKey: idempotencyKey,
            participants: CreateParticipants(messageId));
    }

    public static Message CreateTemplateMessage(Guid messageId)
    {
        var variablesJson = JsonDocument.Parse("""{"name":"Test","orderId":42}""").RootElement;

        return Message.CreatePendingApproval(
            id: messageId,
            channel: "email",
            contentSource: MessageContentSource.Template,
            templateKey: "welcome-email",
            templateVersion: "2.1",
            templateResolvedAt: DateTimeOffset.UtcNow,
            subject: "Welcome",
            textBody: "Hello {{name}}",
            htmlBody: "<p>Hello {{name}}</p>",
            templateVariables: variablesJson,
            participants: CreateParticipants(messageId));
    }

    public static Message CreateMessageWithoutParticipants(Guid messageId)
    {
        return Message.CreatePendingApproval(
            id: messageId,
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: null,
            textBody: "Short message",
            htmlBody: null,
            templateVariables: (JsonElement?)null,
            participants: null);
    }

    public static IReadOnlyList<MessageParticipant> CreateParticipants(Guid messageId)
    {
        return new List<MessageParticipant>
        {
            new MessageParticipant(
                id: Guid.NewGuid(),
                messageId: messageId,
                role: MessageParticipantRole.Sender,
                address: "sender@example.com",
                displayName: "Sender",
                createdAt: DateTimeOffset.UtcNow),

            new MessageParticipant(
                id: Guid.NewGuid(),
                messageId: messageId,
                role: MessageParticipantRole.To,
                address: "to@example.com",
                displayName: "To",
                createdAt: DateTimeOffset.UtcNow)
        };
    }

    public static MessageAuditEvent CreateAuditEvent(
        Guid messageId,
        MessageStatus? fromStatus,
        MessageStatus? toStatus,
        string eventType,
        JsonElement? metadata = null)
    {
        return new MessageAuditEvent(
            id: Guid.NewGuid(),
            messageId: messageId,
            eventType: eventType,
            fromStatus: fromStatus,
            toStatus: toStatus,
            actorType: "System",
            actorId: "test",
            occurredAt: DateTimeOffset.UtcNow,
            metadataJson: metadata);
    }

    public static MessageReview CreateApprovedReview(Guid messageId)
    {
        return new MessageReview(
            id: Guid.NewGuid(),
            messageId: messageId,
            decision: ReviewDecision.Approved,
            decidedBy: "reviewer",
            decidedAt: DateTimeOffset.UtcNow,
            notes: "ok");
    }

    public static MessageReview CreateRejectedReview(Guid messageId)
    {
        return new MessageReview(
            id: Guid.NewGuid(),
            messageId: messageId,
            decision: ReviewDecision.Rejected,
            decidedBy: "reviewer",
            decidedAt: DateTimeOffset.UtcNow,
            notes: "no");
    }

}
