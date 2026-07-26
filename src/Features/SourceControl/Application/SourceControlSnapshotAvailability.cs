namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Availability state for one passive source-control snapshot.
/// </summary>
public enum SourceControlSnapshotAvailability
{
    NoWorkspace,
    NotARepository,
    Available,
    Failed,
}
