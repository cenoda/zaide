using System;

namespace Zaide.Features.Workspace.Domain;

/// <summary>
/// Typed identity for one active workspace instance within the application lifetime.
/// </summary>
internal readonly struct WorkspaceIdentity : IEquatable<WorkspaceIdentity>
{
    private readonly string? _value;

    private WorkspaceIdentity(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static WorkspaceIdentity New() => new($"workspace:{Guid.NewGuid():N}");

    public static WorkspaceIdentity FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workspace identity value is required.", nameof(value));
        }

        if (!value.StartsWith("workspace:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workspace identity must start with 'workspace:'.",
                nameof(value));
        }

        return new WorkspaceIdentity(value);
    }

    public bool Equals(WorkspaceIdentity other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is WorkspaceIdentity other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(WorkspaceIdentity left, WorkspaceIdentity right) => left.Equals(right);

    public static bool operator !=(WorkspaceIdentity left, WorkspaceIdentity right) => !left.Equals(right);

    public override string ToString() => Value;
}
