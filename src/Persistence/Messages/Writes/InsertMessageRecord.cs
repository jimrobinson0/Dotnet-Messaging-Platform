using System.Text.Json;
using Messaging.Core;

namespace Messaging.Persistence.Messages.Writes;

internal sealed record InsertMessageRecord(
    string Channel,
    MessageStatus Status,
    bool RequiresApproval,
    MessageContentSource ContentSource,
    string? TemplateKey,
    string? TemplateVersion,
    DateTimeOffset? TemplateResolvedAt,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    JsonElement? TemplateVariables,
    string IdempotencyKey,
    Guid? ReplyToMessageId,
    string? InReplyTo,
    string? ReferencesHeader);
