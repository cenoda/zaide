using System;

namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Typed identity for one admitted Phase 21 durable record envelope.
/// </summary>
internal readonly struct AgentDurableRecordId : IEquatable<AgentDurableRecordId>
{
    private readonly string? _value;

    private AgentDurableRecordId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentDurableRecordId New() => new($"durable-record:{Guid.NewGuid():N}");

    public static AgentDurableRecordId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Durable record id value is required.", nameof(value));
        }

        return new AgentDurableRecordId(value);
    }

    public bool Equals(AgentDurableRecordId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentDurableRecordId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentDurableRecordId left, AgentDurableRecordId right) =>
        left.Equals(right);

    public static bool operator !=(AgentDurableRecordId left, AgentDurableRecordId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
