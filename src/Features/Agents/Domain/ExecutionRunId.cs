using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Factory-only typed identity for one admitted agent execution attempt.
/// </summary>
public readonly struct ExecutionRunId : IEquatable<ExecutionRunId>
{
    private readonly string? _value;

    private ExecutionRunId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static ExecutionRunId New() => new($"run:{Guid.NewGuid():N}");

    public bool Equals(ExecutionRunId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ExecutionRunId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(ExecutionRunId left, ExecutionRunId right) => left.Equals(right);

    public static bool operator !=(ExecutionRunId left, ExecutionRunId right) => !left.Equals(right);

    public override string ToString() => Value;
}
