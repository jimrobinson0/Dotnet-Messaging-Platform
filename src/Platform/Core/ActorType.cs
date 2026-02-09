namespace Messaging.Platform.Core;

public sealed record ActorType
{
    public static readonly ActorType Human = new("Human");
    public static readonly ActorType Worker = new("Worker");
    public static readonly ActorType System = new("System");

    private ActorType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsHuman => this == Human;

    public static ActorType Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.Equals(raw, Human.Value, StringComparison.OrdinalIgnoreCase)) return Human;
        if (string.Equals(raw, Worker.Value, StringComparison.OrdinalIgnoreCase)) return Worker;
        if (string.Equals(raw, System.Value, StringComparison.OrdinalIgnoreCase)) return System;

        throw new ArgumentException($"Unknown actor type: '{raw}'.", nameof(raw));
    }

    public override string ToString()
    {
        return Value;
    }
}