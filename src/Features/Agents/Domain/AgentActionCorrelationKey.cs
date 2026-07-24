using System;
using System.Text;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Optional backend-supplied opaque correlation key scoped to one run.
/// </summary>
internal readonly struct AgentActionCorrelationKey : IEquatable<AgentActionCorrelationKey>
{
    private readonly string? _value;

    private AgentActionCorrelationKey(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentActionCorrelationKey FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Correlation key value is required.", nameof(value));
        }

        if (AgentActionBudgets.GetUtf8ByteCount(value) > AgentActionBudgets.BackendCorrelationKeyMaxBytes)
        {
            throw new ArgumentException(
                "Correlation key exceeds the maximum UTF-8 byte length.",
                nameof(value));
        }

        return new AgentActionCorrelationKey(value);
    }

    public bool Equals(AgentActionCorrelationKey other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentActionCorrelationKey other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentActionCorrelationKey left, AgentActionCorrelationKey right) =>
        left.Equals(right);

    public static bool operator !=(AgentActionCorrelationKey left, AgentActionCorrelationKey right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
