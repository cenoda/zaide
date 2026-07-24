using System;

namespace Zaide.Features.Workspace.Domain;

/// <summary>
/// Monotonic workspace generation used to invalidate stale action authority.
/// </summary>
internal readonly struct WorkspaceGeneration : IEquatable<WorkspaceGeneration>
{
    public WorkspaceGeneration(ulong value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Workspace generation must be positive.");
        }

        Value = value;
    }

    public ulong Value { get; }

    public static WorkspaceGeneration Initial => new(1);

    public WorkspaceGeneration Next() => new(Value + 1);

    public bool Equals(WorkspaceGeneration other) => Value == other.Value;

    public override bool Equals(object? obj) =>
        obj is WorkspaceGeneration other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(WorkspaceGeneration left, WorkspaceGeneration right) => left.Equals(right);

    public static bool operator !=(WorkspaceGeneration left, WorkspaceGeneration right) => !left.Equals(right);

    public override string ToString() => Value.ToString();
}
