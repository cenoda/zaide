namespace Zaide.Services;

/// <summary>
/// Narrow mutation seam for git stage and unstage operations. Separate from
/// the read-only <see cref="IGitRepositoryService"/> and the refresh-only
/// <see cref="ISourceControlSnapshotOrchestrator"/>. Uses LibGit2Sharp directly.
/// Commit is out of scope for this seam's current implementation (see Phase
/// 7.4 M2); only staging operations are exposed here.
/// </summary>
public interface IGitMutationService
{
    /// <summary>
    /// Stages <paramref name="filePath"/> (relative to <paramref name="repositoryRoot"/>)
    /// in the repository index. A true no-op (returns success) when the file is
    /// already staged.
    /// </summary>
    StageResult Stage(string repositoryRoot, string filePath);

    /// <summary>
    /// Unstages <paramref name="filePath"/> (relative to <paramref name="repositoryRoot"/>)
    /// from the repository index. A true no-op (returns success) when the file is
    /// already unstaged.
    /// </summary>
    StageResult Unstage(string repositoryRoot, string filePath);
}
