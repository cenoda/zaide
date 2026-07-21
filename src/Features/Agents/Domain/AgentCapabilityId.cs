using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed capability identity within one backend capability snapshot.
/// </summary>
internal readonly struct AgentCapabilityId : IEquatable<AgentCapabilityId>
{
    private readonly string? _value;

    private AgentCapabilityId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentCapabilityId MessageCompletion { get; } =
        FromValue("capability:message-completion");

    public static AgentCapabilityId Streaming { get; } =
        FromValue("capability:streaming");

    public static AgentCapabilityId Cancellation { get; } =
        FromValue("capability:cancellation");

    public static AgentCapabilityId Tools { get; } =
        FromValue("capability:tools");

    public static AgentCapabilityId Permissions { get; } =
        FromValue("capability:permissions");

    public static AgentCapabilityId Resume { get; } =
        FromValue("capability:resume");

    public static AgentCapabilityId Reconnect { get; } =
        FromValue("capability:reconnect");

    public static AgentCapabilityId UsageReporting { get; } =
        FromValue("capability:usage-reporting");

    public static AgentCapabilityId RawTrace { get; } =
        FromValue("capability:raw-trace");

    public static AgentCapabilityId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Capability id value is required.", nameof(value));
        }

        if (!value.StartsWith("capability:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Capability id values must use the capability: prefix.",
                nameof(value));
        }

        return new AgentCapabilityId(value);
    }

    public bool Equals(AgentCapabilityId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentCapabilityId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentCapabilityId left, AgentCapabilityId right) =>
        left.Equals(right);

    public static bool operator !=(AgentCapabilityId left, AgentCapabilityId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
