using System.Text.Json;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Exceptions;

namespace Messaging.Platform.Persistence.Messages;

internal static class MessageMapper
{
    public static Message RehydrateMessage(
        MessageRow row,
        IReadOnlyList<MessageParticipantRow> participantRows)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(participantRows);

        var status = ParseEnum<MessageStatus>(row.Status, "message_status");
        var contentSource = ParseEnum<MessageContentSource>(row.ContentSource, "message_content_source");

        var participants = participantRows.Count == 0
            ? Array.Empty<MessageParticipant>()
            : participantRows
                .Select(MapParticipant)
                .ToArray();

        return new Message(
            id: row.Id,
            channel: row.Channel,
            status: status,
            contentSource: contentSource,
            createdAt: row.CreatedAt,
            updatedAt: row.UpdatedAt,
            claimedBy: row.ClaimedBy,
            claimedAt: row.ClaimedAt,
            sentAt: row.SentAt,
            failureReason: row.FailureReason,
            attemptCount: row.AttemptCount,
            templateKey: row.TemplateKey,
            templateVersion: row.TemplateVersion,
            templateResolvedAt: row.TemplateResolvedAt,
            subject: row.Subject,
            textBody: row.TextBody,
            htmlBody: row.HtmlBody,
            templateVariables: ParseJson(row.TemplateVariablesJson),
            idempotencyKey: row.IdempotencyKey,
            participants: participants);
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
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static TEnum ParseEnum<TEnum>(string? raw, string columnName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new PersistenceException($"Column '{columnName}' contains an empty value.");
        }

        if (!Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value))
        {
            throw new PersistenceException($"Column '{columnName}' contains unknown value '{raw}'.");
        }

        return value;
    }
}

internal sealed class MessageRow
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
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
    public string? IdempotencyKey { get; set; }
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
