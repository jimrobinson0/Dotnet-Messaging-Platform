using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Messages;

internal static class MessageCreateIntentMapper
{
    public static MessageCreateIntent ToCreateIntent(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageCreateIntent(
            message.Channel,
            message.Status,
            message.ContentSource,
            message.TemplateKey,
            message.TemplateVersion,
            message.TemplateResolvedAt,
            message.Subject,
            message.TextBody,
            message.HtmlBody,
            message.TemplateVariables,
            message.IdempotencyKey,
            ReplyToMessageId: null,
            InReplyTo: null,
            ReferencesHeader: null);
    }
}
