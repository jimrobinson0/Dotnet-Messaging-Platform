using Messaging.Core;

namespace Messaging.Persistence.Messages.Writes;

internal static class InsertMessageRecordMapper
{
    public static InsertMessageRecord ToInsertRecord(Message message, bool requiresApprovalFromRequest)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new InsertMessageRecord(
            message.Channel,
            message.Status,
            requiresApprovalFromRequest,
            message.ContentSource,
            message.TemplateKey,
            message.TemplateVersion,
            message.TemplateResolvedAt,
            message.Subject,
            message.TextBody,
            message.HtmlBody,
            message.TemplateVariables,
            message.IdempotencyKey,
            message.ReplyToMessageId,
            null,
            null);
    }
}
