using System.Text.Json;
using System.Text.Json.Serialization;

namespace Messaging.Api.Contracts.Messages;

public sealed class MessageResponse
{
    public Guid Id { get; init; }
    public string Channel { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string ContentSource { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? ClaimedBy { get; init; }
    public DateTimeOffset? ClaimedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public string? FailureReason { get; init; }
    public int AttemptCount { get; init; }
    public string? TemplateKey { get; init; }
    public string? TemplateVersion { get; init; }
    public DateTimeOffset? TemplateResolvedAt { get; init; }
    public string? Subject { get; init; }
    public string? TextBody { get; init; }
    public string? HtmlBody { get; init; }
    public required string IdempotencyKey { get; init; }
    public JsonElement? TemplateVariables { get; init; }
    [JsonPropertyName("reply_to_message_id")] public Guid? ReplyToMessageId { get; init; }
    [JsonPropertyName("in_reply_to")] public string? InReplyTo { get; init; }
    [JsonPropertyName("references_header")] public string? ReferencesHeader { get; init; }
    [JsonPropertyName("smtp_message_id")] public string? SmtpMessageId { get; init; }

    public IReadOnlyList<MessageParticipantResponse> Participants { get; init; } =
        Array.Empty<MessageParticipantResponse>();
}

public sealed class MessageParticipantResponse
{
    public Guid Id { get; init; }
    public Guid MessageId { get; init; }
    public string Role { get; init; } = null!;
    public string Address { get; init; } = null!;
    public string? DisplayName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
