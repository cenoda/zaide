using System;
using Zaide.Features.Terminal.Application;

namespace Zaide.Features.Terminal.Contracts;

/// <summary>
/// Passive, read-only terminal snapshot seam for IDE context assembly.
/// </summary>
public interface ITerminalSurfaceSnapshotService : IDisposable
{
    /// <summary>The current immutable terminal surface snapshot.</summary>
    TerminalSurfaceSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="TerminalSurfaceSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<TerminalSurfaceSnapshot> WhenChanged { get; }
}
