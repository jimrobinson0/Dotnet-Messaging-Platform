namespace Messaging.Core;

public readonly record struct ReviewDecisionResult(
    MessageReview Review,
    MessageStatusTransition Transition);