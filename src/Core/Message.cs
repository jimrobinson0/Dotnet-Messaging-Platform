using System.Text.Json;
using Messaging.Core.Exceptions;

namespace Messaging.Core;

/// <summary>
///     Represents a single immutable sendable message and its lifecycle state.
///     This aggregate is the authoritative in-memory representation of a persisted message row.
/// </summary>
public sealed class Message
{
    private const int MaxIdempotencyKeyLength = 128;

    private readonly List<MessageParticipant> _participants;

    /// <summary>
    ///     Rehydration constructor.
    ///     Intended for loading an existing message from persistence.
    /// </summary>
    internal Message(
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
        int attemptCount,
        string? templateKey,
        string? templateVersion,
        DateTimeOffset? templateResolvedAt,
        string? subject,
        string? textBody,
        string? htmlBody,
        JsonElement? templateVariables,
        string? idempotencyKey,
        Guid? replyToMessageId,
        string? inReplyTo,
        string? referencesHeader,
        string? smtpMessageId,
        IEnumerable<MessageParticipant>? participants = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        EnsureTemplateIdentityConstraint(contentSource, templateKey);

        if (attemptCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(attemptCount),
                "Attempt count cannot be negative.");

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
        AttemptCount = attemptCount;
        TemplateKey = templateKey;
        TemplateVersion = templateVersion;
        TemplateResolvedAt = templateResolvedAt;
        Subject = subject;
        TextBody = textBody;
        HtmlBody = htmlBody;
        TemplateVariables = JsonGuard.EnsureCloned(templateVariables, nameof(templateVariables));
        IdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        ReplyToMessageId = replyToMessageId;
        InReplyTo = inReplyTo;
        ReferencesHeader = referencesHeader;
        SmtpMessageId = smtpMessageId;

        _participants = participants is null
            ? []
            : [.. participants];

        Participants = _participants.AsReadOnly();

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
    public int AttemptCount { get; private set; }
    public string? TemplateKey { get; }
    public string? TemplateVersion { get; }
    public DateTimeOffset? TemplateResolvedAt { get; }
    public string? Subject { get; }
    public string? TextBody { get; }
    public string? HtmlBody { get; }
    public JsonElement? TemplateVariables { get; }
    public string? IdempotencyKey { get; }
    public Guid? ReplyToMessageId { get; }
    public string? InReplyTo { get; }
    public string? ReferencesHeader { get; }
    public string? SmtpMessageId { get; }
    public IReadOnlyList<MessageParticipant> Participants { get; }

    /// <summary>
    ///     Creates a message aggregate with an initial status derived from approval requirements.
    ///     CreatedAt / UpdatedAt are populated by persistence.
    /// </summary>
    public static Message Create(MessageCreateSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(spec.Participants);

        return new Message(
            spec.Id,
            spec.Channel,
            spec.RequiresApproval ? MessageStatus.PendingApproval : MessageStatus.Approved,
            spec.ContentSource,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            null,
            null,
            null,
            null,
            0,
            spec.TemplateKey,
            spec.TemplateVersion,
            spec.TemplateResolvedAt,
            spec.Subject,
            spec.TextBody,
            spec.HtmlBody,
            spec.TemplateVariables,
            spec.IdempotencyKey,
            spec.ReplyToMessageId,
            null,
            null,
            null,
            spec.Participants);
    }

    /// <summary>
    ///     Ensures the message is eligible to be claimed by a worker for delivery.
    /// </summary>
    public void EnsureSendable()
    {
        if (!MessageLifecycle.IsSendable(Status))
            throw new InvalidMessageStatusTransitionException(
                Status,
                MessageStatus.Sending);
    }

    /// <summary>
    ///     Ensures the message is in a terminal state.
    /// </summary>
    public void EnsureIsTerminal()
    {
        if (!MessageLifecycle.IsTerminal(Status))
            throw new InvalidOperationException(
                $"Message '{Id}' is not in a terminal status. Current status: '{Status}'.");
    }

    /// <summary>
    ///     Approves a pending message via a human review action.
    ///     Produces both a lifecycle transition and a review record.
    /// </summary>
    public ReviewDecisionResult Approve(
        Guid reviewId,
        string decidedBy,
        DateTimeOffset decidedAt,
        string? notes,
        ActorType actorType)
    {
        ArgumentNullException.ThrowIfNull(decidedBy);

        EnsureReviewActor(actorType);
        EnsureReviewAllowed("Approval");

        var transition = TransitionTo(MessageStatus.Approved, decidedAt);
        var review = new MessageReview(
            reviewId,
            Id,
            ReviewDecision.Approved,
            decidedBy,
            decidedAt,
            notes);

        return new ReviewDecisionResult(review, transition);
    }

    /// <summary>
    ///     Rejects a pending message via a human review action.
    ///     Produces both a lifecycle transition and a review record.
    /// </summary>
    public ReviewDecisionResult Reject(
        Guid reviewId,
        string decidedBy,
        DateTimeOffset decidedAt,
        string? notes,
        ActorType actorType)
    {
        ArgumentNullException.ThrowIfNull(decidedBy);

        EnsureReviewActor(actorType);
        EnsureReviewAllowed("Rejection");

        var transition = TransitionTo(MessageStatus.Rejected, decidedAt);
        var review = new MessageReview(
            reviewId,
            Id,
            ReviewDecision.Rejected,
            decidedBy,
            decidedAt,
            notes);

        return new ReviewDecisionResult(review, transition);
    }

    /// <summary>
    ///     Claims the message for delivery by a worker.
    /// </summary>
    public MessageStatusTransition StartSending(
        string claimedBy,
        DateTimeOffset claimedAt)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);

        var transition = TransitionTo(MessageStatus.Sending, claimedAt);
        ClaimedBy = claimedBy;
        ClaimedAt = claimedAt;

        return transition;
    }

    /// <summary>
    ///     Records a successful delivery attempt.
    /// </summary>
    public MessageStatusTransition RecordSendSuccess(DateTimeOffset sentAt)
    {
        AttemptCount += 1;

        var transition = TransitionTo(MessageStatus.Sent, sentAt);

        SentAt = sentAt;
        FailureReason = null;

        return transition;
    }


    /// <summary>
    ///     Records a failed delivery attempt and determines retry or terminal failure.
    /// </summary>
    public MessageStatusTransition RecordSendAttemptFailure(
        int maxAttempts,
        string? failureReason,
        DateTimeOffset failedAt)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                "Max attempts must be greater than zero.");

        AttemptCount += 1;

        var nextStatus = AttemptCount < maxAttempts
            ? MessageStatus.Approved
            : MessageStatus.Failed;

        var transition = TransitionTo(nextStatus, failedAt);

        FailureReason = failureReason;
        SentAt = null;

        return transition;
    }

    /// <summary>
    ///     Cancels the message from any non-terminal state.
    /// </summary>
    public MessageStatusTransition Cancel(DateTimeOffset canceledAt)
    {
        return TransitionTo(MessageStatus.Canceled, canceledAt);
    }

    /// <summary>
    ///     Transitions the message to a new status and records the logical event time.
    /// </summary>
    /// <remarks>
    ///     UpdatedAt represents the logical time of the state transition as observed by the caller.
    ///     Persisted timestamps are assigned by the database and may differ.
    ///     After persistence, the in-memory aggregate is considered dirty with respect to DB-owned timestamps.
    ///     See Persistence/README.md — Timestamp Ownership.
    /// </remarks>
    private MessageStatusTransition TransitionTo(
        MessageStatus toStatus,
        DateTimeOffset occurredAt)
    {
        var fromStatus = Status;

        MessageLifecycle.EnsureValidTransition(fromStatus, toStatus);

        Status = toStatus;
        UpdatedAt = occurredAt;

        return new MessageStatusTransition(
            Id,
            fromStatus,
            toStatus,
            occurredAt);
    }

    private static void EnsureTemplateIdentityConstraint(
        MessageContentSource contentSource,
        string? templateKey)
    {
        if (contentSource == MessageContentSource.Template && templateKey is null)
            throw new InvalidOperationException(
                "Template content requires template_key to be non-null.");

        if (contentSource == MessageContentSource.Direct && templateKey is not null)
            throw new InvalidOperationException(
                "Direct content requires template_key to be null.");
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (idempotencyKey is null) return null;

        var normalized = idempotencyKey.Trim();
        if (normalized.Length == 0) return null;

        if (normalized.Length > MaxIdempotencyKeyLength)
            throw new ArgumentException(
                $"Idempotency key cannot exceed {MaxIdempotencyKeyLength} characters.",
                nameof(idempotencyKey));

        return normalized;
    }

    private void EnsureParticipantMessageIds()
    {
        foreach (var participant in _participants) EnsureParticipantBelongs(participant);
    }

    private void EnsureParticipantBelongs(MessageParticipant participant)
    {
        if (participant.MessageId != Id)
            throw new ArgumentException(
                $"Participant message_id '{participant.MessageId}' does not match message '{Id}'.",
                nameof(participant));
    }

    private static void EnsureReviewActor(ActorType actorType)
    {
        if (!actorType.IsHuman)
            throw new ApprovalRuleViolationException(
                "Approval may only be performed by human actors.");
    }

    private void EnsureReviewAllowed(string actionLabel)
    {
        if (Status != MessageStatus.PendingApproval)
            throw new ApprovalRuleViolationException(
                $"{actionLabel} is only allowed when status is '{MessageStatus.PendingApproval}'. Current status: '{Status}'.");
    }
}
