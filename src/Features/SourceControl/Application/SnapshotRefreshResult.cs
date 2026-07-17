namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Outcome of a Source Control snapshot refresh request. Truthfully projects the
/// three possible states of requesting a fresh repository snapshot for a workspace:
/// a successful read, a workspace that is not inside a repository, and an unexpected
/// failure (IO/permissions). No fake/demo data is ever produced through this seam.
/// </summary>
public enum SnapshotRefreshStatus
{
    /// <summary>A fresh repository snapshot was read successfully.</summary>
    Success,

    /// <summary>The workspace path is not inside a git repository (truthful empty).</summary>
    NotARepository,

    /// <summary>An unexpected failure prevented reading a snapshot (e.g. IO/permissions).</summary>
    Failed,
}

/// <summary>
/// Passive result of <see cref="ISourceControlSnapshotOrchestrator.Refresh"/>. Carries
/// the read snapshot only on <see cref="SnapshotRefreshStatus.Success"/>; otherwise it
/// carries an optional human-readable <see cref="ErrorMessage"/>. It is not a source
/// of truth — the <see cref="IGitRepositoryService"/> owns the truth.
/// </summary>
public sealed class SnapshotRefreshResult
{
    /// <summary>The projected outcome of the refresh request.</summary>
    public SnapshotRefreshStatus Status { get; init; }

    /// <summary>The workspace path the refresh was requested for, when available.</summary>
    public string? WorkspacePath { get; init; }

    /// <summary>The read snapshot, set only when <see cref="Status"/> is <see cref="SnapshotRefreshStatus.Success"/>.</summary>
    public RepositoryStatusSnapshot? Snapshot { get; init; }

    /// <summary>Optional human-readable error, set when <see cref="Status"/> is <see cref="SnapshotRefreshStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; init; }

    public static SnapshotRefreshResult Success(string? workspacePath, RepositoryStatusSnapshot snapshot) =>
        new() { Status = SnapshotRefreshStatus.Success, WorkspacePath = workspacePath, Snapshot = snapshot };

    public static SnapshotRefreshResult NotARepository(string? workspacePath, string? message = null) =>
        new() { Status = SnapshotRefreshStatus.NotARepository, WorkspacePath = workspacePath, ErrorMessage = message };

    public static SnapshotRefreshResult Failed(string? workspacePath, string errorMessage) =>
        new() { Status = SnapshotRefreshStatus.Failed, WorkspacePath = workspacePath, ErrorMessage = errorMessage };
}
