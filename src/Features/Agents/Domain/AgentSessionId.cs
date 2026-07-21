using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed identity for one in-memory Agent Session.
/// </summary>
internal readonly struct AgentSessionId : IEquatable<AgentSessionId>
{
    private readonly string? _value;

    private AgentSessionId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentSessionId New() => new($"session:{Guid.NewGuid():N}");

    public static AgentSessionId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Session id value is required.", nameof(value));
        }

        return new AgentSessionId(value);
    }

    public bool Equals(AgentSessionId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentSessionId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentSessionId left, AgentSessionId right) => left.Equals(right);

    public static bool operator !=(AgentSessionId left, AgentSessionId right) => !left.Equals(right);

    public override string ToString() => Value;
}
