using System;
using System.Reactive.Subjects;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Application;

/// <summary>
/// Passive, read-only editor snapshot owner for IDE context assembly.
/// Starts empty until editor presentation publishes live state.
/// </summary>
internal sealed class EditorStateSnapshotService
    : IEditorStateSnapshotService, IEditorStateSnapshotPublisher
{
    private readonly Subject<EditorStateSnapshot> _subject = new();
    private readonly object _gate = new();
    private EditorStateSnapshot _current = new(generation: 0);
    private bool _disposed;

    public EditorStateSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public IObservable<EditorStateSnapshot> WhenChanged => _subject;

    public bool TryPublish(EditorStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            var generation = snapshot.Generation == 0
                ? _current.Generation + 1
                : snapshot.Generation;

            if (generation <= _current.Generation)
            {
                return false;
            }

            var published = new EditorStateSnapshot(
                generation,
                snapshot.ActiveFilePath,
                snapshot.ActiveFileContent,
                snapshot.ActiveFileIsDirty,
                snapshot.OpenFilePaths,
                snapshot.CaretLine,
                snapshot.CaretColumn,
                snapshot.SelectionStart,
                snapshot.SelectionLength,
                snapshot.SelectionText);

            _current = published;
            _subject.OnNext(published);
            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _subject.OnCompleted();
        _subject.Dispose();
    }
}
