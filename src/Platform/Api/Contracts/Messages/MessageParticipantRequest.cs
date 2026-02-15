using System.ComponentModel.DataAnnotations;

namespace Messaging.Platform.Api.Contracts.Messages;

public sealed class MessageParticipantRequest
{
    [Required][MaxLength(50)] public string Role { get; init; } = string.Empty;

    [Required][MaxLength(500)] public string Address { get; init; } = string.Empty;

    [MaxLength(255)] public string? DisplayName { get; init; }
}