using System.Text.Json;

namespace Messaging.Core;

public sealed record MessageCreateSpec(
    Guid Id,
    string Channel,
    MessageContentSource ContentSource,
    bool RequiresApproval,
    string? TemplateKey,
    string? TemplateVersion,
    DateTimeOffset? TemplateResolvedAt,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    JsonElement? TemplateVariables,
    string? IdempotencyKey,
    IReadOnlyList<MessageParticipant> Participants,
    Guid? ReplyToMessageId);
