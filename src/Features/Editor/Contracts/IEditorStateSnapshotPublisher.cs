using Zaide.Features.Editor.Application;

namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Feature-owned publication seam for live editor snapshots. Consumers of
/// <see cref="IEditorStateSnapshotService"/> remain read-only.
/// </summary>
public interface IEditorStateSnapshotPublisher
{
    /// <summary>
    /// Publishes immutable editor state. When <paramref name="snapshot"/>.Generation
    /// is zero, the service assigns the next monotonic generation. Otherwise the
    /// provided generation must exceed the current generation. Returns false
    /// after disposal or when the update is stale.
    /// </summary>
    bool TryPublish(EditorStateSnapshot snapshot);
}
