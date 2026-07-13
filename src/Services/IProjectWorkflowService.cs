using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// UI-independent orchestration for one build/run/test managed process at a time.
/// Consumes only <see cref="IProjectContextService"/> snapshots for target resolution.
/// </summary>
public interface IProjectWorkflowService : IDisposable
{
    /// <summary>The current immutable workflow snapshot.</summary>
    ProjectWorkflowSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="ProjectWorkflowSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<ProjectWorkflowSnapshot> WhenChanged { get; }

    /// <summary>
    /// Emits captured stdout/stderr lines for later Output consumers.
    /// </summary>
    IObservable<ManagedProcessOutputLine> WhenOutputReceived { get; }

    /// <summary>Starts <c>dotnet build &lt;file&gt;</c> for the selected project.</summary>
    Task<ProjectWorkflowOperationResult> StartBuildAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts <c>dotnet run --project &lt;file&gt;</c> for eligible C# projects only.
    /// </summary>
    Task<ProjectWorkflowOperationResult> StartRunAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Starts <c>dotnet test &lt;file&gt;</c> for the selected project.</summary>
    Task<ProjectWorkflowOperationResult> StartTestAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the active operation and kills the managed process tree.
    /// </summary>
    Task CancelAsync(CancellationToken cancellationToken = default);
}
