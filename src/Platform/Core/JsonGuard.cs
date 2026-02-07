using System.Text.Json;

namespace Messaging.Platform.Core;

/// <summary>
/// Defensive cloning for <see cref="JsonElement"/> values stored in domain aggregates.
/// Prevents mutation of shared JsonDocument-backed elements after construction.
/// </summary>
internal static class JsonGuard
{
    public static JsonElement? EnsureCloned(JsonElement? json, string parameterName)
    {
        if (json is null)
        {
            return null;
        }

        var value = json.Value;

        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException(
                $"{parameterName} must be valid JSON.",
                parameterName);
        }

        return value.Clone();
    }
}
