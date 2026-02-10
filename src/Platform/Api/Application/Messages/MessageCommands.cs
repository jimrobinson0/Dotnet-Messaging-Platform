using System.Text.Json;
using Messaging.Platform.Core;

namespace Messaging.Platform.Api.Application.Messages;

public sealed record CreateMessageCommand(
    string Channel,
    string ContentSource,
    bool RequiresApproval,
    string? TemplateKey,
    string? TemplateVersion,
    DateTimeOffset? TemplateResolvedAt,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    JsonElement? TemplateVariables,
    string? IdempotencyKey,
    IReadOnlyList<MessageParticipantInput> Participants,
    string ActorType,
    string ActorId,
    Guid? ActorUserId);

public sealed record CreateMessageResult(
    Message Message,
    bool WasCreated);

public sealed record MessageParticipantInput(
    string Role,
    string Address,
    string? DisplayName);

public sealed record ReviewMessageCommand(
    string DecidedBy,
    string ActorType,
    string? ActorId,
    Guid? ActorUserId,
    DateTimeOffset? DecidedAt,
    string? Notes);
