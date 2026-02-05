namespace Messaging.Platform.Core;

public sealed record ChannelType
{
    public string Value { get; }

    private ChannelType(string value) => Value = value;

    public static readonly ChannelType Email = new("email");

    public static ChannelType Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.Equals(raw, Email.Value, StringComparison.OrdinalIgnoreCase))
            return Email;

        throw new ArgumentException($"Unknown channel type: '{raw}'.", nameof(raw));
    }

    public override string ToString() => Value;
}