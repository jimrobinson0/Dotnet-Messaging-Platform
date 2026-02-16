using System.Text.Json;
using Messaging.Core;
using Messaging.Persistence.Exceptions;

namespace Messaging.Persistence.Messages.Mapping;

internal static class MessageMapper
{
    public static Message RehydrateMessage(MessageRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return RehydrateMessage(row, Array.Empty<MessageParticipantRow>());
    }

    public static Message RehydrateMessage(
        MessageRow row,
        IReadOnlyList<MessageParticipantRow> participantRows)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(participantRows);

        var contentSource = ParseEnum<MessageContentSource>(row.ContentSource, "message_content_source");

        var participants = participantRows.Count == 0
            ? Array.Empty<MessageParticipant>()
            : participantRows
                .Select(MapParticipant)
                .ToArray();

        return new Message(
            row.Id,
            row.Channel,
            row.Status,
            contentSource,
            row.CreatedAt,
            row.UpdatedAt,
            row.ClaimedBy,
            row.ClaimedAt,
            row.SentAt,
            row.FailureReason,
            row.AttemptCount,
            row.TemplateKey,
            row.TemplateVersion,
            row.TemplateResolvedAt,
            row.Subject,
            row.TextBody,
            row.HtmlBody,
            ParseJson(row.TemplateVariablesJson ?? row.TemplateVariables),
            row.IdempotencyKey,
            row.ReplyToMessageId,
            row.InReplyTo,
            row.ReferencesHeader,
            row.SmtpMessageId,
            participants);
    }

    public static string? SerializeJson(JsonElement? json)
    {
        return json?.GetRawText();
    }

    private static MessageParticipant MapParticipant(MessageParticipantRow row)
    {
        var role = ParseEnum<MessageParticipantRole>(row.Role, "message_participant_role");

        return new MessageParticipant(
            row.Id,
            row.MessageId,
            role,
            row.Address,
            row.DisplayName,
            row.CreatedAt);
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static TEnum ParseEnum<TEnum>(string? raw, string columnName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new PersistenceException($"Column '{columnName}' contains an empty value.");

        if (!Enum.TryParse<TEnum>(raw, true, out var value))
            throw new PersistenceException($"Column '{columnName}' contains unknown value '{raw}'.");

        return value;
    }
}

internal sealed class MessageRow
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public MessageStatus Status { get; set; }
    public string ContentSource { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? FailureReason { get; set; }
    public int AttemptCount { get; set; }
    public string? TemplateKey { get; set; }
    public string? TemplateVersion { get; set; }
    public DateTimeOffset? TemplateResolvedAt { get; set; }
    public string? Subject { get; set; }
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public string? TemplateVariablesJson { get; set; }
    public string? TemplateVariables { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public string? InReplyTo { get; set; }
    public string? ReferencesHeader { get; set; }
    public string? SmtpMessageId { get; set; }
}

internal sealed class MessageParticipantRow
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
