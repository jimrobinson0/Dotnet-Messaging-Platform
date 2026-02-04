namespace Messaging.Platform.Core.Exceptions;

public sealed class ApprovalRuleViolationException : Exception
{
    public ApprovalRuleViolationException(string message)
        : base(message)
    {
    }
}
