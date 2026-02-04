using Messaging.Platform.Core;

namespace Messaging.Platform.Core.Exceptions;

public sealed class FrozenMessageContentException : Exception
{
    public FrozenMessageContentException(Guid messageId, MessageStatus status, string? fieldName = null)
        : base(BuildMessage(messageId, status, fieldName))
    {
        MessageId = messageId;
        Status = status;
        FieldName = fieldName;
    }

    public Guid MessageId { get; }
    public MessageStatus Status { get; }
    public string? FieldName { get; }

    private static string BuildMessage(Guid messageId, MessageStatus status, string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return $"Message content is frozen for message '{messageId}' in status '{status}'.";
        }

        return $"Message content '{fieldName}' is frozen for message '{messageId}' in status '{status}'.";
    }
}
