using System;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Stable identity for one durable scoped memory record across revisions.
/// </summary>
internal readonly struct AgentMemoryId : IEquatable<AgentMemoryId>
{
    private readonly string? _value;

    private AgentMemoryId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentMemoryId New() => new($"memory:{Guid.NewGuid():N}");

    public static AgentMemoryId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Memory id value is required.", nameof(value));
        }

        return new AgentMemoryId(value);
    }

    public bool Equals(AgentMemoryId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentMemoryId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentMemoryId left, AgentMemoryId right) =>
        left.Equals(right);

    public static bool operator !=(AgentMemoryId left, AgentMemoryId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
