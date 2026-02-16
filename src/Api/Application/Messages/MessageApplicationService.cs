using System.Text.Json;
using Messaging.Core;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Messages;

namespace Messaging.Api.Application.Messages;

public sealed class MessageApplicationService : IMessageApplicationService
{
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
        var createSpec = new MessageCreateSpec(
            provisionalMessageId,
            channel,
            contentSource,
            command.RequiresApproval,
            command.TemplateKey,
            command.TemplateVersion,
            command.TemplateResolvedAt,
            command.Subject,
            command.TextBody,
            command.HtmlBody,
            command.TemplateVariables,
            command.IdempotencyKey,
            participants,
            command.ReplyToMessageId);
        var message = Message.Create(createSpec);

        var createIntent = MessageCreateIntentMapper.ToCreateIntent(message, command.RequiresApproval);
        var participantPrototypes = ParticipantPrototypeMapper.FromCore(message.Participants);

        var actorType = ParseActorType(command.ActorType);
        var actorId = RequireValue(command.ActorId, nameof(command.ActorId));
        Func<Guid, MessageAuditEvent> auditEventFactory = persistedMessageId => new MessageAuditEvent(
            Guid.NewGuid(),
            persistedMessageId,
            AuditEventTypes.MessageCreated,
            fromStatus: null,
            message.Status,
            actorType.ToString(),
            actorId,
            occurredAt: DateTimeOffset.UtcNow,
            metadataJson: JsonSerializer.SerializeToElement(new { command.RequiresApproval }));

        MessageCreateResult result = await _messageRepository.CreateAsync(
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
            Guid.NewGuid(),
            messageId,
            eventType,
            fromStatus: null,
            toStatus: null,
            actorType.ToString(),
            actorId,
            decidedAt,
            JsonSerializer.SerializeToElement(
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
        if (participants.Count == 0) return Array.Empty<MessageParticipant>();

        var createdAt = DateTimeOffset.UtcNow;
        var mappedParticipants = new List<MessageParticipant>(participants.Count);

        foreach (var participant in participants)
            mappedParticipants.Add(new MessageParticipant(
                Guid.NewGuid(),
                messageId,
                ParseParticipantRole(participant.Role),
                RequireValue(participant.Address, nameof(participant.Address)),
                participant.DisplayName,
                createdAt));

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
        if (Enum.TryParse<MessageContentSource>(value, true, out var contentSource)) return contentSource;

        throw new ArgumentException($"Unknown message content source: '{value}'.", nameof(raw));
    }

    private static MessageParticipantRole ParseParticipantRole(string raw)
    {
        var value = RequireValue(raw, nameof(raw));
        if (Enum.TryParse<MessageParticipantRole>(value, true, out var role)) return role;

        throw new ArgumentException($"Unknown participant role: '{value}'.", nameof(raw));
    }

    private static ActorType ParseActorType(string raw)
    {
        return ActorType.Parse(RequireValue(raw, nameof(raw)));
    }

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);

        return value;
    }
}
