using System;
using System.Reactive.Subjects;
using Zaide.Features.SourceControl.Contracts;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Passive, read-only source-control snapshot owner for IDE context assembly.
/// Starts unavailable until source-control presentation publishes live state.
/// </summary>
internal sealed class SourceControlSnapshotService
    : ISourceControlSnapshotService, ISourceControlSnapshotPublisher
{
    private readonly Subject<SourceControlStatusSnapshot> _subject = new();
    private readonly object _gate = new();
    private SourceControlStatusSnapshot _current = new(
        generation: 0,
        availability: SourceControlSnapshotAvailability.NoWorkspace);
    private bool _disposed;

    public SourceControlStatusSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public IObservable<SourceControlStatusSnapshot> WhenChanged => _subject;

    public bool TryPublish(SourceControlStatusSnapshot snapshot)
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

            var published = new SourceControlStatusSnapshot(
                generation,
                snapshot.Availability,
                snapshot.WorkspacePath,
                snapshot.RepositoryStatus,
                snapshot.StatusMessage);

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
