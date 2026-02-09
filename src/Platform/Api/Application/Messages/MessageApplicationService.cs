using System.Text.Json;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Messages;

namespace Messaging.Platform.Api.Application.Messages;

public sealed class MessageApplicationService : IMessageApplicationService
{
    private const int MaxListLimit = 500;

    private readonly MessageRepository _messageRepository;

    public MessageApplicationService(
        MessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<CreateMessageResult> CreateAsync(
        CreateMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provisionalMessageId = Guid.NewGuid();
        var channel = ParseChannel(command.Channel);
        var contentSource = ParseContentSource(command.ContentSource);
        var participants = BuildParticipants(provisionalMessageId, command.Participants);

        var message = command.RequiresApproval
            ? Message.CreatePendingApproval(
                id: provisionalMessageId,
                channel: channel,
                contentSource: contentSource,
                templateKey: command.TemplateKey,
                templateVersion: command.TemplateVersion,
                templateResolvedAt: command.TemplateResolvedAt,
                subject: command.Subject,
                textBody: command.TextBody,
                htmlBody: command.HtmlBody,
                templateVariables: command.TemplateVariables,
                idempotencyKey: command.IdempotencyKey,
                participants: participants)
            : Message.CreateApproved(
                id: provisionalMessageId,
                channel: channel,
                contentSource: contentSource,
                templateKey: command.TemplateKey,
                templateVersion: command.TemplateVersion,
                templateResolvedAt: command.TemplateResolvedAt,
                subject: command.Subject,
                textBody: command.TextBody,
                htmlBody: command.HtmlBody,
                templateVariables: command.TemplateVariables,
                idempotencyKey: command.IdempotencyKey,
                participants: participants);

        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message);
        var participantPrototypes = ParticipantPrototypeMapper.FromCore(message.Participants);

        var actorType = ParseActorType(command.ActorType);
        var actorId = RequireValue(command.ActorId, nameof(command.ActorId));
        Func<Guid, MessageAuditEvent> auditEventFactory = persistedMessageId => new MessageAuditEvent(
            id: Guid.NewGuid(),
            messageId: persistedMessageId,
            eventType: AuditEventTypes.MessageCreated,
            fromStatus: null,
            toStatus: message.Status,
            actorType: actorType.ToString(),
            actorId: actorId,
            occurredAt: DateTimeOffset.UtcNow,
            metadataJson: JsonSerializer.SerializeToElement(new { command.RequiresApproval }));

        var result = await _messageRepository.CreateAsync(
            createIntent,
            participantPrototypes,
            auditEventFactory,
            cancellationToken);

        return new CreateMessageResult(result.Message, result.WasCreated);
    }

    public async Task<Message> ApproveAsync(
        Guid messageId,
        ReviewMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ApplyReviewAsync(
            messageId,
            command,
            AuditEventTypes.MessageApproved,
            ReviewDecision.Approved,
            (message, reviewId, decidedBy, decidedAt, notes, actorType) =>
                message.Approve(reviewId, decidedBy, decidedAt, notes, actorType),
            cancellationToken);
    }

    public async Task<Message> RejectAsync(
        Guid messageId,
        ReviewMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ApplyReviewAsync(
            messageId,
            command,
            AuditEventTypes.MessageRejected,
            ReviewDecision.Rejected,
            (message, reviewId, decidedBy, decidedAt, notes, actorType) =>
                message.Reject(reviewId, decidedBy, decidedAt, notes, actorType),
            cancellationToken);
    }

    public async Task<Message> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return await _messageRepository.GetByIdAsync(messageId, cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListAsync(
        string? status,
        int limit,
        DateTimeOffset? createdAfter,
        CancellationToken cancellationToken = default)
    {
        var parsedStatus = ParseOptionalStatus(status);

        if (limit <= 0 || limit > MaxListLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaxListLimit}.");
        }

        return await _messageRepository.ListAsync(parsedStatus, limit, createdAfter, cancellationToken);
    }

    private async Task<Message> ApplyReviewAsync(
        Guid messageId,
        ReviewMessageCommand command,
        string eventType,
        ReviewDecision decision,
        Func<Message, Guid, string, DateTimeOffset, string?, ActorType, ReviewDecisionResult> applyDecision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var decidedBy = RequireValue(command.DecidedBy, nameof(command.DecidedBy));
        var actorType = ParseActorType(command.ActorType);
        var actorId = string.IsNullOrWhiteSpace(command.ActorId)
            ? decidedBy
            : command.ActorId;
        var decidedAt = command.DecidedAt ?? DateTimeOffset.UtcNow;

        var audit = new MessageAuditEvent(
            id: Guid.NewGuid(),
            messageId: messageId,
            eventType: eventType,
            fromStatus: null,
            toStatus: null,
            actorType: actorType.ToString(),
            actorId: actorId,
            occurredAt: decidedAt,
            metadataJson: JsonSerializer.SerializeToElement(
                new
                {
                    decision = decision.ToString(),
                    command.Notes
                }));

        return await _messageRepository.ApplyReviewAsync(
            messageId,
            message => applyDecision(
                message,
                Guid.NewGuid(),
                decidedBy,
                decidedAt,
                command.Notes,
                actorType),
            audit,
            cancellationToken);
    }

    private static IReadOnlyList<MessageParticipant> BuildParticipants(
        Guid messageId,
        IReadOnlyList<MessageParticipantInput> participants)
    {
        if (participants.Count == 0)
        {
            return Array.Empty<MessageParticipant>();
        }

        var createdAt = DateTimeOffset.UtcNow;
        var mappedParticipants = new List<MessageParticipant>(participants.Count);

        foreach (var participant in participants)
        {
            mappedParticipants.Add(new MessageParticipant(
                id: Guid.NewGuid(),
                messageId: messageId,
                role: ParseParticipantRole(participant.Role),
                address: RequireValue(participant.Address, nameof(participant.Address)),
                displayName: participant.DisplayName,
                createdAt: createdAt));
        }

        return mappedParticipants;
    }

    private static string ParseChannel(string raw)
    {
        var channel = ChannelType.Parse(RequireValue(raw, nameof(raw)));
        return channel.Value;
    }

    private static MessageContentSource ParseContentSource(string raw)
    {
        var value = RequireValue(raw, nameof(raw));
        if (Enum.TryParse<MessageContentSource>(value, ignoreCase: true, out var contentSource))
        {
            return contentSource;
        }

        throw new ArgumentException($"Unknown message content source: '{value}'.", nameof(raw));
    }

    private static MessageParticipantRole ParseParticipantRole(string raw)
    {
        var value = RequireValue(raw, nameof(raw));
        if (Enum.TryParse<MessageParticipantRole>(value, ignoreCase: true, out var role))
        {
            return role;
        }

        throw new ArgumentException($"Unknown participant role: '{value}'.", nameof(raw));
    }

    private static ActorType ParseActorType(string raw)
    {
        return ActorType.Parse(RequireValue(raw, nameof(raw)));
    }

    private static MessageStatus? ParseOptionalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<MessageStatus>(status, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"Unknown status value '{status}'.",
            nameof(status));
    }

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value;
    }
}
