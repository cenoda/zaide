using System;
using System.Reactive.Subjects;
using Zaide.Features.SourceControl.Contracts;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Passive, read-only source-control snapshot owner for IDE context assembly.
/// Starts unavailable until source-control presentation publishes live state.
/// </summary>
internal sealed class SourceControlSnapshotService : ISourceControlSnapshotService
{
    private readonly Subject<SourceControlStatusSnapshot> _subject = new();
    private SourceControlStatusSnapshot _current = new(
        generation: 0,
        availability: SourceControlSnapshotAvailability.NoWorkspace);
    private bool _disposed;

    public SourceControlStatusSnapshot Current => _current;

    public IObservable<SourceControlStatusSnapshot> WhenChanged => _subject;

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
