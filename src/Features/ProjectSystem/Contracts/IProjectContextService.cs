using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Singleton service that owns the authoritative <see cref="ProjectContext"/>
/// for the currently opened workspace.
///
/// <para>Lifecycle methods accept cancellation. Cancellation is never
/// represented as <see cref="ProjectContextState.Failed"/> — a cancelled
/// operation restores the last stable snapshot.</para>
/// </summary>
public interface IProjectContextService : IDisposable
{
    /// <summary>
    /// The current immutable project-context snapshot.
    /// </summary>
    ProjectContext Current { get; }

    /// <summary>
    /// An observable that emits each new <see cref="ProjectContext"/> snapshot
    /// on the calling thread. UI consumers must marshal via
    /// <c>ObserveOn(RxApp.MainThreadScheduler)</c>.
    /// </summary>
    IObservable<ProjectContext> WhenChanged { get; }

    /// <summary>
    /// Load the project context for the given <paramref name="workspaceRoot"/>.
    /// After a cancellation check, emits <c>Loading</c>, then runs discovery
    /// and emits a terminal snapshot.
    /// </summary>
    Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-scan the current workspace root. Requires a non-null current root;
    /// otherwise emits a terminal <see cref="ProjectContextState.Failed"/>
    /// snapshot with <see cref="ProjectDiscoveryFailureKind.InvalidRoot"/>.
    /// </summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unload the current workspace and return to <c>Unloaded</c> state.
    /// </summary>
    Task UnloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Select a candidate from the current snapshot's <c>Candidates</c> list.
    /// A null candidate clears the selection. A candidate not in the current
    /// snapshot is rejected with a Warning log (event ID 8303).
    /// </summary>
    void SelectProject(ProjectCandidate? candidate);
}
