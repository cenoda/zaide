using System;
using System.IO;

namespace Zaide.Features.Workspace.Domain;

/// <summary>
/// Immutable capture of the active workspace identity, generation, and canonical
/// root path bound to one admitted action attempt. The scope is captured once
/// and re-validated against the live workspace generation before execution so
/// that a workspace close/switch invalidates stale action authority.
/// </summary>
internal sealed class WorkspaceActionScope : IEquatable<WorkspaceActionScope>
{
    public WorkspaceActionScope(
        WorkspaceIdentity identity,
        WorkspaceGeneration generation,
        string rootPath)
    {
        if (identity == default)
        {
            throw new ArgumentException("Workspace identity is required.", nameof(identity));
        }

        if (generation == default)
        {
            throw new ArgumentException("Workspace generation is required.", nameof(generation));
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Workspace root path is required.", nameof(rootPath));
        }

        if (!Path.IsPathRooted(rootPath))
        {
            throw new ArgumentException(
                "Workspace root path must be absolute.",
                nameof(rootPath));
        }

        Identity = identity;
        Generation = generation;
        RootPath = rootPath;
    }

    public WorkspaceIdentity Identity { get; }

    public WorkspaceGeneration Generation { get; }

    /// <summary>
    /// Absolute captured workspace root. The read adapter canonicalizes this
    /// value against the live filesystem before evaluating path containment.
    /// </summary>
    public string RootPath { get; }

    public bool Equals(WorkspaceActionScope? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Identity == other.Identity
            && Generation == other.Generation
            && string.Equals(RootPath, other.RootPath, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as WorkspaceActionScope);

    public override int GetHashCode() =>
        HashCode.Combine(Identity, Generation, StringComparer.Ordinal.GetHashCode(RootPath));
}
