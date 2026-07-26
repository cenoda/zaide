using System;
using Zaide.Features.Editor.Application;

namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Passive, read-only editor snapshot seam for IDE context assembly.
/// </summary>
public interface IEditorStateSnapshotService : IDisposable
{
    /// <summary>The current immutable editor snapshot.</summary>
    EditorStateSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="EditorStateSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<EditorStateSnapshot> WhenChanged { get; }
}
