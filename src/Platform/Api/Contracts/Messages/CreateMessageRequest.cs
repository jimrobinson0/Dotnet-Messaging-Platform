using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Messaging.Platform.Api.Contracts.Messages;

public sealed class CreateMessageRequest
{
    [Required]
    [MaxLength(50)]
    public string Channel { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ContentSource { get; init; } = string.Empty;

    public bool RequiresApproval { get; init; }

    [MaxLength(255)]
    public string? TemplateKey { get; init; }

    [MaxLength(100)]
    public string? TemplateVersion { get; init; }

    public DateTimeOffset? TemplateResolvedAt { get; init; }

    [MaxLength(1000)]
    public string? Subject { get; init; }

    [MaxLength(100_000)]
    public string? TextBody { get; init; }

    [MaxLength(500_000)]
    public string? HtmlBody { get; init; }

    public JsonElement? TemplateVariables { get; init; }

    [MaxLength(128)]
    public string? IdempotencyKey { get; init; }

    public IReadOnlyList<MessageParticipantRequest> Participants { get; init; } = Array.Empty<MessageParticipantRequest>();

    [Required]
    [MaxLength(50)]
    public string ActorType { get; init; } = "System";

    [Required]
    [MaxLength(255)]
    public string ActorId { get; init; } = "api";
}
