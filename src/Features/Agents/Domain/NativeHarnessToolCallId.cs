using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Run-scoped identifier for one model-issued tool call within the Native Harness loop.
/// Distinct from <see cref="AgentActionId"/> and broker correlation keys.
/// </summary>
internal readonly struct NativeHarnessToolCallId : IEquatable<NativeHarnessToolCallId>
{
    private readonly string? _value;

    private NativeHarnessToolCallId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static NativeHarnessToolCallId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Tool call id value is required.", nameof(value));
        }

        return new NativeHarnessToolCallId(value.Trim());
    }

    public bool Equals(NativeHarnessToolCallId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is NativeHarnessToolCallId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(NativeHarnessToolCallId left, NativeHarnessToolCallId right) =>
        left.Equals(right);

    public static bool operator !=(NativeHarnessToolCallId left, NativeHarnessToolCallId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
