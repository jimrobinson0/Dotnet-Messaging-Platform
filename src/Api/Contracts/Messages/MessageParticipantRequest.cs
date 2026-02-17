using System.ComponentModel.DataAnnotations;

namespace Messaging.Api.Contracts.Messages;

public sealed class MessageParticipantRequest
{
    [Required][MaxLength(50)] public string Role { get; init; } = null!;

    [Required][MaxLength(500)] public string Address { get; init; } = null!;

    [MaxLength(255)] public string? DisplayName { get; init; }
}
