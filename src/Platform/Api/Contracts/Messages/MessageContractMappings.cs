using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Core;

namespace Messaging.Platform.Api.Contracts.Messages;

public static class MessageContractMappings
{
    public static CreateMessageCommand ToCommand(this CreateMessageRequest request, string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(request);

        var participants = (request.Participants ?? Array.Empty<MessageParticipantRequest>())
            .Select(participant => new MessageParticipantInput(
                participant.Role,
                participant.Address,
                participant.DisplayName))
            .ToArray();

        return new CreateMessageCommand(
            Channel: request.Channel,
            ContentSource: request.ContentSource,
            RequiresApproval: request.RequiresApproval,
            TemplateKey: request.TemplateKey,
            TemplateVersion: request.TemplateVersion,
            TemplateResolvedAt: request.TemplateResolvedAt,
            Subject: request.Subject,
            TextBody: request.TextBody,
            HtmlBody: request.HtmlBody,
            TemplateVariables: request.TemplateVariables,
            IdempotencyKey: idempotencyKey,
            Participants: participants,
            ActorType: request.ActorType,
            ActorId: request.ActorId);
    }

    public static ReviewMessageCommand ToCommand(this ReviewMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ReviewMessageCommand(
            DecidedBy: request.DecidedBy,
            ActorType: request.ActorType,
            ActorId: request.ActorId,
            DecidedAt: request.DecidedAt,
            Notes: request.Notes);
    }

    public static MessageResponse ToResponse(this Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var participants = message.Participants
            .Select(participant => new MessageParticipantResponse
            {
                Id = participant.Id,
                MessageId = participant.MessageId,
                Role = participant.Role.ToString(),
                Address = participant.Address,
                DisplayName = participant.DisplayName,
                CreatedAt = participant.CreatedAt
            })
            .ToArray();

        return new MessageResponse
        {
            Id = message.Id,
            Channel = message.Channel,
            Status = message.Status.ToString(),
            ContentSource = message.ContentSource.ToString(),
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            ClaimedBy = message.ClaimedBy,
            ClaimedAt = message.ClaimedAt,
            SentAt = message.SentAt,
            FailureReason = message.FailureReason,
            AttemptCount = message.AttemptCount,
            TemplateKey = message.TemplateKey,
            TemplateVersion = message.TemplateVersion,
            TemplateResolvedAt = message.TemplateResolvedAt,
            Subject = message.Subject,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
            IdempotencyKey = message.IdempotencyKey,
            TemplateVariables = message.TemplateVariables is { } templateVariables
                ? templateVariables.Clone()
                : null,
            Participants = participants
        };
    }
}
