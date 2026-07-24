using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Zaide-owned identity for one admitted action request.
/// </summary>
internal readonly struct AgentActionId : IEquatable<AgentActionId>
{
    private readonly string? _value;

    private AgentActionId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentActionId New() => new($"action:{Guid.NewGuid():N}");

    public static AgentActionId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Action id value is required.", nameof(value));
        }

        if (!value.StartsWith("action:", StringComparison.Ordinal))
        {
            throw new ArgumentException("Action id must start with 'action:'.", nameof(value));
        }

        return new AgentActionId(value);
    }

    public bool Equals(AgentActionId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentActionId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentActionId left, AgentActionId right) => left.Equals(right);

    public static bool operator !=(AgentActionId left, AgentActionId right) => !left.Equals(right);

    public override string ToString() => Value;
}
