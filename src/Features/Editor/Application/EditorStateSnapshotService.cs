using System;
using System.Reactive.Subjects;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Application;

/// <summary>
/// Passive, read-only editor snapshot owner for IDE context assembly.
/// Starts empty until editor presentation publishes live state.
/// </summary>
internal sealed class EditorStateSnapshotService : IEditorStateSnapshotService
{
    private readonly Subject<EditorStateSnapshot> _subject = new();
    private EditorStateSnapshot _current = new(generation: 0);
    private bool _disposed;

    public EditorStateSnapshot Current => _current;

    public IObservable<EditorStateSnapshot> WhenChanged => _subject;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subject.OnCompleted();
        _subject.Dispose();
    }
}
