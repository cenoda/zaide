namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Maps orchestrator refresh results into passive source-control snapshots.
/// </summary>
internal static class SourceControlSnapshotMapper
{
    public static SourceControlStatusSnapshot FromRefreshResult(
        SnapshotRefreshResult result,
        string? presentationStatusMessage)
    {
        var availability = ResolveAvailability(result);
        var statusMessage = availability == SourceControlSnapshotAvailability.Available
            ? null
            : presentationStatusMessage ?? result.ErrorMessage;

        return new SourceControlStatusSnapshot(
            generation: 0,
            availability: availability,
            workspacePath: result.WorkspacePath,
            repositoryStatus: result.Snapshot,
            statusMessage: statusMessage);
    }

    private static SourceControlSnapshotAvailability ResolveAvailability(
        SnapshotRefreshResult result)
    {
        if (string.IsNullOrEmpty(result.WorkspacePath))
        {
            return SourceControlSnapshotAvailability.NoWorkspace;
        }

        return result.Status switch
        {
            SnapshotRefreshStatus.Success => SourceControlSnapshotAvailability.Available,
            SnapshotRefreshStatus.NotARepository => SourceControlSnapshotAvailability.NotARepository,
            SnapshotRefreshStatus.Failed => SourceControlSnapshotAvailability.Failed,
            _ => SourceControlSnapshotAvailability.Failed,
        };
    }
}
