using Messaging.Platform.Core.Exceptions;

namespace Messaging.Platform.Core;

public sealed class Message
{
    private readonly List<MessageParticipant> _participants;
    private readonly IReadOnlyList<MessageParticipant> _participantsReadOnly;

    public Message(
        Guid id,
        string channel,
        MessageStatus status,
        MessageContentSource contentSource,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? claimedBy,
        DateTimeOffset? claimedAt,
        DateTimeOffset? sentAt,
        string? failureReason,
        string? templateKey,
        string? templateVersion,
        DateTimeOffset? templateResolvedAt,
        string? subject,
        string? textBody,
        string? htmlBody,
        string? templateVariables,
        IEnumerable<MessageParticipant>? participants = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        EnsureTemplateIdentityConstraint(contentSource, templateKey);

        Id = id;
        Channel = channel;
        Status = status;
        ContentSource = contentSource;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ClaimedBy = claimedBy;
        ClaimedAt = claimedAt;
        SentAt = sentAt;
        FailureReason = failureReason;
        TemplateKey = templateKey;
        TemplateVersion = templateVersion;
        TemplateResolvedAt = templateResolvedAt;
        Subject = subject;
        TextBody = textBody;
        HtmlBody = htmlBody;
        TemplateVariables = templateVariables;

        _participants = participants is null ? new List<MessageParticipant>() : new List<MessageParticipant>(participants);
        _participantsReadOnly = _participants.AsReadOnly();

        EnsureParticipantMessageIds();
    }

    public Guid Id { get; }
    public string Channel { get; }
    public MessageStatus Status { get; private set; }
    public MessageContentSource ContentSource { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? ClaimedBy { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? FailureReason { get; private set; }
    public string? TemplateKey { get; private set; }
    public string? TemplateVersion { get; private set; }
    public DateTimeOffset? TemplateResolvedAt { get; private set; }
    public string? Subject { get; private set; }
    public string? TextBody { get; private set; }
    public string? HtmlBody { get; private set; }
    public string? TemplateVariables { get; private set; }
    public IReadOnlyList<MessageParticipant> Participants => _participantsReadOnly;

    public void EnsureMutable()
    {
        if (!MessageLifecycle.IsContentMutable(Status))
        {
            throw new FrozenMessageContentException(Id, Status);
        }
    }

    public void EnsureSendable()
    {
        if (!MessageLifecycle.IsSendable(Status))
        {
            throw new InvalidMessageStatusTransitionException(Status, MessageStatus.Sending);
        }
    }

    public void EnsureTerminal()
    {
        if (!MessageLifecycle.IsTerminal(Status))
        {
            throw new InvalidOperationException(
                $"Message '{Id}' is not in a terminal status. Current status: '{Status}'.");
        }
    }

    public void UpdateContent(string? subject, string? textBody, string? htmlBody, DateTimeOffset updatedAt)
    {
        EnsureMutable();

        Subject = subject;
        TextBody = textBody;
        HtmlBody = htmlBody;
        UpdatedAt = updatedAt;
    }

    public void SetTemplateIdentity(
        string? templateKey,
        string? templateVersion,
        DateTimeOffset? templateResolvedAt,
        DateTimeOffset updatedAt)
    {
        EnsureMutable();
        EnsureTemplateIdentityConstraint(ContentSource, templateKey);

        TemplateKey = templateKey;
        TemplateVersion = templateVersion;
        TemplateResolvedAt = templateResolvedAt;
        UpdatedAt = updatedAt;
    }

    public void SetTemplateVariables(string? templateVariables, DateTimeOffset updatedAt)
    {
        EnsureMutable();

        TemplateVariables = templateVariables;
        UpdatedAt = updatedAt;
    }

    public void ReplaceParticipants(IEnumerable<MessageParticipant> participants, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(participants);
        EnsureMutable();

        var replacement = new List<MessageParticipant>(participants);
        foreach (var participant in replacement)
        {
            EnsureParticipantBelongs(participant);
        }

        _participants.Clear();
        _participants.AddRange(replacement);
        UpdatedAt = updatedAt;
    }

    public void AddParticipant(MessageParticipant participant, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(participant);
        EnsureMutable();
        EnsureParticipantBelongs(participant);

        _participants.Add(participant);
        UpdatedAt = updatedAt;
    }

    public bool RemoveParticipant(Guid participantId, DateTimeOffset updatedAt)
    {
        EnsureMutable();

        var index = _participants.FindIndex(participant => participant.Id == participantId);
        if (index < 0)
        {
            return false;
        }

        _participants.RemoveAt(index);
        UpdatedAt = updatedAt;
        return true;
    }

    public MessageStatusTransition Queue(DateTimeOffset queuedAt)
    {
        return TransitionTo(MessageStatus.Queued, queuedAt);
    }

    public MessageStatusTransition RequestApproval(DateTimeOffset requestedAt)
    {
        return TransitionTo(MessageStatus.PendingApproval, requestedAt);
    }

    public ReviewDecisionResult ApplyReviewDecision(
        Guid reviewId,
        ReviewDecision decision,
        string decidedBy,
        DateTimeOffset decidedAt,
        string? notes,
        string actorType)
    {
        ArgumentNullException.ThrowIfNull(decidedBy);
        EnsureApprovalActor(actorType);

        var targetStatus = decision == ReviewDecision.Approved
            ? MessageStatus.Approved
            : MessageStatus.Rejected;

        var transition = TransitionTo(targetStatus, decidedAt);
        var review = new MessageReview(reviewId, Id, decision, decidedBy, decidedAt, notes);
        return new ReviewDecisionResult(review, transition);
    }

    public MessageStatusTransition StartSending(string claimedBy, DateTimeOffset claimedAt)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);

        var transition = TransitionTo(MessageStatus.Sending, claimedAt);
        ClaimedBy = claimedBy;
        ClaimedAt = claimedAt;
        return transition;
    }

    public MessageStatusTransition MarkSent(DateTimeOffset sentAt)
    {
        var transition = TransitionTo(MessageStatus.Sent, sentAt);
        SentAt = sentAt;
        FailureReason = null;
        return transition;
    }

    public MessageStatusTransition MarkFailed(string? failureReason, DateTimeOffset failedAt)
    {
        var transition = TransitionTo(MessageStatus.Failed, failedAt);
        FailureReason = failureReason;
        SentAt = null;
        return transition;
    }

    public MessageStatusTransition Cancel(DateTimeOffset canceledAt)
    {
        return TransitionTo(MessageStatus.Canceled, canceledAt);
    }

    private MessageStatusTransition TransitionTo(MessageStatus toStatus, DateTimeOffset occurredAt)
    {
        var fromStatus = Status;
        MessageLifecycle.EnsureValidTransition(fromStatus, toStatus);

        Status = toStatus;
        UpdatedAt = occurredAt;

        return new MessageStatusTransition(Id, fromStatus, toStatus, occurredAt);
    }

    private static void EnsureTemplateIdentityConstraint(MessageContentSource contentSource, string? templateKey)
    {
        if (contentSource == MessageContentSource.Template && templateKey is null)
        {
            throw new InvalidOperationException(
                "Template content requires template_key to be non-null.");
        }

        if (contentSource == MessageContentSource.Direct && templateKey is not null)
        {
            throw new InvalidOperationException(
                "Direct content requires template_key to be null.");
        }
    }

    private void EnsureParticipantMessageIds()
    {
        foreach (var participant in _participants)
        {
            EnsureParticipantBelongs(participant);
        }
    }

    private void EnsureParticipantBelongs(MessageParticipant participant)
    {
        if (participant.MessageId != Id)
        {
            throw new ArgumentException(
                $"Participant message_id '{participant.MessageId}' does not match message '{Id}'.",
                nameof(participant));
        }
    }

    private static void EnsureApprovalActor(string actorType)
    {
        ArgumentNullException.ThrowIfNull(actorType);

        if (string.Equals(actorType, "Worker", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApprovalRuleViolationException("Workers cannot originate approval transitions.");
        }
    }
}
