using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Zaide-owned identity for one execution attempt of an admitted action.
/// </summary>
internal readonly struct AgentActionAttemptId : IEquatable<AgentActionAttemptId>
{
    private readonly string? _value;

    private AgentActionAttemptId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentActionAttemptId New() => new($"action-attempt:{Guid.NewGuid():N}");

    public static AgentActionAttemptId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Action attempt id value is required.", nameof(value));
        }

        if (!value.StartsWith("action-attempt:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Action attempt id must start with 'action-attempt:'.",
                nameof(value));
        }

        return new AgentActionAttemptId(value);
    }

    public bool Equals(AgentActionAttemptId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentActionAttemptId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentActionAttemptId left, AgentActionAttemptId right) =>
        left.Equals(right);

    public static bool operator !=(AgentActionAttemptId left, AgentActionAttemptId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
