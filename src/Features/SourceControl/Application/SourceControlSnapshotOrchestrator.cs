using System;
using Zaide.Features.SourceControl.Contracts;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Read-only orchestration over <see cref="IGitRepositoryService"/> that turns a workspace
/// path into a <see cref="SnapshotRefreshResult"/>. It is the single explicit refresh seam
/// for Source Control: discover the repository root, read its status, and project success,
/// non-repo, or failure truthfully. No mutation or UI behavior is introduced here.
/// </summary>
public sealed class SourceControlSnapshotOrchestrator : ISourceControlSnapshotOrchestrator
{
    private readonly IGitRepositoryService _git;

    public SourceControlSnapshotOrchestrator(IGitRepositoryService git)
    {
        _git = git;
    }

    /// <inheritdoc/>
    public SnapshotRefreshResult Refresh(string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return SnapshotRefreshResult.NotARepository(workspacePath, "No workspace is open.");
        }

        RepositoryDiscoveryResult discovery;
        try
        {
            discovery = _git.Discover(workspacePath!);
        }
        catch (Exception ex)
        {
            return SnapshotRefreshResult.Failed(workspacePath, ex.Message);
        }

        if (!discovery.IsRepository || discovery.RepositoryRoot is null)
        {
            return SnapshotRefreshResult.NotARepository(workspacePath);
        }

        try
        {
            var snapshot = _git.ReadStatus(discovery.RepositoryRoot);
            return SnapshotRefreshResult.Success(workspacePath, snapshot);
        }
        catch (Exception ex)
        {
            return SnapshotRefreshResult.Failed(workspacePath, ex.Message);
        }
    }
}
