using System.Text.Json;
using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Messages;

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
    string? IdempotencyKey);