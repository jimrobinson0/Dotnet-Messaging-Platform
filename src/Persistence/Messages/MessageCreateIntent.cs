using System.Text.Json;
using Messaging.Core;

namespace Messaging.Persistence.Messages;

public sealed record MessageCreateIntent(
    string Channel,
    MessageStatus Status,
    MessageContentSource ContentSource,
    string? TemplateKey,
    string? TemplateVersion,
    DateTimeOffset? TemplateResolvedAt,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    JsonElement? TemplateVariables,
    string? IdempotencyKey,
    Guid? ReplyToMessageId,
    string? InReplyTo,
    string? ReferencesHeader);
