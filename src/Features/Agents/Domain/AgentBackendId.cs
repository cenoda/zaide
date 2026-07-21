using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed backend identity for one Agent Session binding.
/// </summary>
internal readonly struct AgentBackendId : IEquatable<AgentBackendId>
{
    private readonly string? _value;

    private AgentBackendId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentBackendId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Backend id value is required.", nameof(value));
        }

        if (!value.StartsWith("backend:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Backend id values must use the backend: prefix.",
                nameof(value));
        }

        return new AgentBackendId(value);
    }

    public bool Equals(AgentBackendId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentBackendId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentBackendId left, AgentBackendId right) => left.Equals(right);

    public static bool operator !=(AgentBackendId left, AgentBackendId right) => !left.Equals(right);

    public override string ToString() => Value;
}
