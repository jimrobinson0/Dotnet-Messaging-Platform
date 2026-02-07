using System.ComponentModel.DataAnnotations;

namespace Messaging.Platform.Api.Contracts.Messages;

public sealed class ReviewMessageRequest
{
    [Required]
    [MaxLength(255)]
    public string DecidedBy { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ActorType { get; init; } = "Human";

    [MaxLength(255)]
    public string? ActorId { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    [MaxLength(10_000)]
    public string? Notes { get; init; }
}
