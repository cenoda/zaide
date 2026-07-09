namespace Zaide.Services;

/// <summary>
/// Narrow mutation seam for git stage, unstage, and commit operations.
/// Separate from the read-only <see cref="IGitRepositoryService"/> and the
/// refresh-only <see cref="ISourceControlSnapshotOrchestrator"/>. Uses
/// LibGit2Sharp directly. Returns result types instead of throwing across
/// the seam boundary.
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

    /// <summary>
    /// Creates a local commit in the repository at <paramref name="repositoryRoot"/>
    /// with the given <paramref name="message"/>. Validates the message is non-empty,
    /// that at least one change is staged, and that a git identity is configured
    /// before attempting the commit.
    /// </summary>
    CommitResult Commit(string repositoryRoot, string message);
}
