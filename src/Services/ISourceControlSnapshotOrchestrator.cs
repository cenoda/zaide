namespace Zaide.Services;

/// <summary>
/// Narrow app-orchestration seam for Source Control snapshots (M3). Requests a fresh
/// repository snapshot for a workspace path from the read-only <see cref="IGitRepositoryService"/>
/// truth seam, projecting success, non-repo, and failure states truthfully. It performs
/// no rendering, staging, committing, or branch switching — it only turns a workspace
/// path into a <see cref="SnapshotRefreshResult"/>.
/// </summary>
public interface ISourceControlSnapshotOrchestrator
{
    /// <summary>
    /// Requests a fresh snapshot for the workspace at <paramref name="workspacePath"/>.
    /// Never throws for non-repo or transient failures; those are returned as
    /// <see cref="SnapshotRefreshStatus.NotARepository"/> / <see cref="SnapshotRefreshStatus.Failed"/>.
    /// </summary>
    SnapshotRefreshResult Refresh(string? workspacePath);
}
