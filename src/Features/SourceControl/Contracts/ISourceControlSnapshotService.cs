using System;
using Zaide.Features.SourceControl.Application;

namespace Zaide.Features.SourceControl.Contracts;

/// <summary>
/// Passive, read-only source-control snapshot seam for IDE context assembly.
/// Replaces active <c>Refresh()</c> polling for context consumers.
/// </summary>
public interface ISourceControlSnapshotService : IDisposable
{
    /// <summary>The current immutable source-control snapshot.</summary>
    SourceControlStatusSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="SourceControlStatusSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<SourceControlStatusSnapshot> WhenChanged { get; }
}
