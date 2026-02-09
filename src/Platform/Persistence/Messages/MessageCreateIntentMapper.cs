using Messaging.Platform.Core;

namespace Messaging.Platform.Persistence.Messages;

internal static class MessageCreateIntentMapper
{
    public static MessageCreateIntent ToCreateIntent(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageCreateIntent(
            Channel: message.Channel,
            Status: message.Status,
            ContentSource: message.ContentSource,
            TemplateKey: message.TemplateKey,
            TemplateVersion: message.TemplateVersion,
            TemplateResolvedAt: message.TemplateResolvedAt,
            Subject: message.Subject,
            TextBody: message.TextBody,
            HtmlBody: message.HtmlBody,
            TemplateVariables: message.TemplateVariables,
            IdempotencyKey: message.IdempotencyKey);
    }
}
