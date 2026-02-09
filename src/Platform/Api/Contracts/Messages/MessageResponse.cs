using System.Text.Json;

namespace Messaging.Platform.Api.Contracts.Messages;

public sealed class MessageResponse
{
    public Guid Id { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ContentSource { get; init; } = string.Empty;
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
    public string? IdempotencyKey { get; init; }
    public JsonElement? TemplateVariables { get; init; }

    public IReadOnlyList<MessageParticipantResponse> Participants { get; init; } =
        Array.Empty<MessageParticipantResponse>();
}

public sealed class MessageParticipantResponse
{
    public Guid Id { get; init; }
    public Guid MessageId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}