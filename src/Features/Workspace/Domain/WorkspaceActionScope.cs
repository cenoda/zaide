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
        string rootPath,
        string capturedCanonicalRoot,
        ulong capturedRootDevice,
        ulong capturedRootInode)
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

        if (string.IsNullOrWhiteSpace(capturedCanonicalRoot))
        {
            throw new ArgumentException(
                "Captured canonical root is required.",
                nameof(capturedCanonicalRoot));
        }

        if (!Path.IsPathRooted(capturedCanonicalRoot))
        {
            throw new ArgumentException(
                "Captured canonical root must be absolute.",
                nameof(capturedCanonicalRoot));
        }

        if (capturedRootDevice == 0)
        {
            throw new ArgumentException(
                "Captured root device is required for root identity validation.",
                nameof(capturedRootDevice));
        }

        if (capturedRootInode == 0)
        {
            throw new ArgumentException(
                "Captured root inode is required for root identity validation.",
                nameof(capturedRootInode));
        }

        Identity = identity;
        Generation = generation;
        RootPath = rootPath;
        CapturedCanonicalRoot = capturedCanonicalRoot;
        CapturedRootDevice = capturedRootDevice;
        CapturedRootInode = capturedRootInode;
    }

    public WorkspaceIdentity Identity { get; }

    public WorkspaceGeneration Generation { get; }

    /// <summary>
    /// Absolute captured workspace root as supplied by the authority. The read
    /// adapter re-canonicalizes this value against the live filesystem and
    /// compares it with <see cref="CapturedCanonicalRoot"/> before evaluating
    /// path containment.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Canonical absolute path of the workspace root resolved at capture time
    /// (e.g. via <c>realpath</c>). The read adapter re-validates this against
    /// the live filesystem before opening any file so that root symlink
    /// retargeting, root replacement, and equivalent TOCTOU cases are detected
    /// without relying only on the generation counter.
    /// </summary>
    public string CapturedCanonicalRoot { get; }

    /// <summary>
    /// Device id of the workspace root directory stat'd at capture time.
    /// Required; the read adapter rejects any scope where this is zero.
    /// Together with <see cref="CapturedRootInode"/> this detects root
    /// directory replacement (same path, different filesystem object).
    /// </summary>
    public ulong CapturedRootDevice { get; }

    /// <summary>
    /// Inode of the workspace root directory stat'd at capture time.
    /// Required; the read adapter rejects any scope where this is zero.
    /// </summary>
    public ulong CapturedRootInode { get; }

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
            && string.Equals(RootPath, other.RootPath, StringComparison.Ordinal)
            && string.Equals(CapturedCanonicalRoot, other.CapturedCanonicalRoot, StringComparison.Ordinal)
            && CapturedRootDevice == other.CapturedRootDevice
            && CapturedRootInode == other.CapturedRootInode;
    }

    public override bool Equals(object? obj) => Equals(obj as WorkspaceActionScope);

    public override int GetHashCode() =>
        HashCode.Combine(
            Identity,
            Generation,
            StringComparer.Ordinal.GetHashCode(RootPath),
            StringComparer.Ordinal.GetHashCode(CapturedCanonicalRoot),
            CapturedRootDevice,
            CapturedRootInode);
}
