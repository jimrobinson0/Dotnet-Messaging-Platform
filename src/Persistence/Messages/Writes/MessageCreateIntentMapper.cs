using Messaging.Core;

namespace Messaging.Persistence.Messages;

internal static class MessageCreateIntentMapper
{
    public static MessageCreateIntent ToCreateIntent(Message message, bool requiresApprovalFromRequest)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageCreateIntent(
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
            null,
            null,
            null);
    }
}
