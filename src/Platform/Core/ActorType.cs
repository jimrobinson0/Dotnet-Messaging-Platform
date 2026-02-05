namespace Messaging.Platform.Core;

public sealed record ActorType
{
    public string Value { get; }

    private ActorType(string value) => Value = value;

    public static readonly ActorType Human = new("Human");
    public static readonly ActorType Worker = new("Worker");
    public static readonly ActorType System = new("System");

    public bool IsHuman => this == Human;

    public static ActorType Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.Equals(raw, Human.Value, StringComparison.OrdinalIgnoreCase)) return Human;
        if (string.Equals(raw, Worker.Value, StringComparison.OrdinalIgnoreCase)) return Worker;
        if (string.Equals(raw, System.Value, StringComparison.OrdinalIgnoreCase)) return System;

        throw new ArgumentException($"Unknown actor type: '{raw}'.", nameof(raw));
    }

    public override string ToString() => Value;
}