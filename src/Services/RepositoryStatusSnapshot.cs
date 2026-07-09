using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Passive snapshot of a discovered repository's branch/HEAD state and
/// working-tree changes. Produced by <see cref="IGitRepositoryService.ReadStatus"/>.
/// Consumed by later phases; not a source of truth on its own.
/// </summary>
public sealed class RepositoryStatusSnapshot
{
    /// <summary>
    /// Current branch friendly name, or the detached commit SHA when detached.
    /// </summary>
    public string CurrentBranchName { get; init; } = string.Empty;

    /// <summary>
    /// True when HEAD points directly at a commit (detached HEAD).
    /// </summary>
    public bool IsDetachedHead { get; init; }

    /// <summary>
    /// All local branches, with <see cref="Zaide.Models.GitBranch.IsCurrent"/>
    /// set on the one matching HEAD (when not detached).
    /// </summary>
    public IReadOnlyList<Zaide.Models.GitBranch> Branches { get; init; }
        = System.Array.Empty<Zaide.Models.GitBranch>();

    /// <summary>
    /// Working-tree file changes (staged + unstaged combined by this read seam).
    /// Later phases split by <see cref="Zaide.Models.FileChange.IsStaged"/>.
    /// </summary>
    public IReadOnlyList<Zaide.Models.FileChange> Changes { get; init; }
        = System.Array.Empty<Zaide.Models.FileChange>();
}