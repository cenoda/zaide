using Zaide.Features.SourceControl.Application;

namespace Zaide.Features.SourceControl.Contracts;

/// <summary>
/// Feature-owned publication seam for live source-control snapshots. Consumers of
/// <see cref="ISourceControlSnapshotService"/> remain read-only.
/// </summary>
public interface ISourceControlSnapshotPublisher
{
    /// <summary>
    /// Publishes immutable source-control state. When <paramref name="snapshot"/>.Generation
    /// is zero, the service assigns the next monotonic generation. Otherwise the
    /// provided generation must exceed the current generation. Returns false
    /// after disposal or when the update is stale.
    /// </summary>
    bool TryPublish(SourceControlStatusSnapshot snapshot);
}
