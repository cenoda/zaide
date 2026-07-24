using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Zaide-owned identity for one permission decision publication.
/// </summary>
internal readonly struct AgentPermissionDecisionId : IEquatable<AgentPermissionDecisionId>
{
    private readonly string? _value;

    private AgentPermissionDecisionId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentPermissionDecisionId New() => new($"permission-decision:{Guid.NewGuid():N}");

    public static AgentPermissionDecisionId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Permission decision id value is required.", nameof(value));
        }

        if (!value.StartsWith("permission-decision:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Permission decision id must start with 'permission-decision:'.",
                nameof(value));
        }

        return new AgentPermissionDecisionId(value);
    }

    public bool Equals(AgentPermissionDecisionId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentPermissionDecisionId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentPermissionDecisionId left, AgentPermissionDecisionId right) =>
        left.Equals(right);

    public static bool operator !=(AgentPermissionDecisionId left, AgentPermissionDecisionId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
