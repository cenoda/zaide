using System.Collections.Generic;

namespace Zaide.Features.SourceControl.Application;

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
    /// All local branches, with <see cref="Zaide.Features.SourceControl.Domain.GitBranch.IsCurrent"/>
    /// set on the one matching HEAD (when not detached).
    /// </summary>
    public IReadOnlyList<Zaide.Features.SourceControl.Domain.GitBranch> Branches { get; init; }
        = System.Array.Empty<Zaide.Features.SourceControl.Domain.GitBranch>();

    /// <summary>
    /// Working-tree file changes (staged + unstaged combined by this read seam).
    /// Later phases split by <see cref="Zaide.Features.SourceControl.Domain.FileChange.IsStaged"/>.
    /// </summary>
    public IReadOnlyList<Zaide.Features.SourceControl.Domain.FileChange> Changes { get; init; }
        = System.Array.Empty<Zaide.Features.SourceControl.Domain.FileChange>();

    /// <summary>
    /// True when the current branch tracks an upstream remote branch.
    /// False for detached HEAD or branches without upstream configuration.
    /// </summary>
    public bool HasUpstream { get; init; }

    /// <summary>
    /// Number of local commits ahead of the tracked upstream branch.
    /// Zero when there is no upstream or the branch is up to date.
    /// </summary>
    public int AheadBy { get; init; }

    /// <summary>
    /// Number of local commits behind the tracked upstream branch.
    /// Zero when there is no upstream or the branch is up to date.
    /// </summary>
    public int BehindBy { get; init; }
}