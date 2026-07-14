using System;

namespace Zaide.Services;

/// <summary>
/// Projects structured build/run/test process output from
/// <see cref="IProjectWorkflowService"/> for read-only UI surfaces.
/// </summary>
public interface IProjectOutputService
{
    /// <summary>The current immutable output snapshot.</summary>
    ProjectOutputSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="ProjectOutputSnapshot"/> on the calling thread.
    /// Snapshots are emitted on state transitions (Starting, Running, Idle) but
    /// not per output line. Use <see cref="WhenLineReceived"/> for per-line deltas.
    /// </summary>
    IObservable<ProjectOutputSnapshot> WhenChanged { get; }

    /// <summary>
    /// Emits each captured stdout/stderr line as it arrives. Subscribe to this for
    /// append-only UI updates instead of rebuilding the full line list from
    /// <see cref="WhenChanged"/> snapshots.
    /// </summary>
    IObservable<ManagedProcessOutputLine> WhenLineReceived { get; }
}
