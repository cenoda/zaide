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
    /// </summary>
    IObservable<ProjectOutputSnapshot> WhenChanged { get; }
}
