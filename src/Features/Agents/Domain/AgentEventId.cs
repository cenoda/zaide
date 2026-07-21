using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed identity for one normalized Agent Session event.
/// </summary>
internal readonly struct AgentEventId : IEquatable<AgentEventId>
{
    private readonly string? _value;

    private AgentEventId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentEventId New() => new($"agent-event:{Guid.NewGuid():N}");

    public static AgentEventId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Event id value is required.", nameof(value));
        }

        return new AgentEventId(value);
    }

    public bool Equals(AgentEventId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentEventId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentEventId left, AgentEventId right) => left.Equals(right);

    public static bool operator !=(AgentEventId left, AgentEventId right) => !left.Equals(right);

    public override string ToString() => Value;
}
