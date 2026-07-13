using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Zaide.Services;

/// <summary>
/// Maps <see cref="IProjectWorkflowService"/> snapshots into structured Output
/// projections. Does not own process execution.
/// </summary>
public sealed class ProjectOutputService : IProjectOutputService, IDisposable
{
    private static readonly ProjectOutputSnapshot Empty = Map(
        new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            Generation: 0,
            ActiveOperation: null,
            LastOutcome: null,
            TargetFilePath: null,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>()));

    private readonly IProjectWorkflowService _workflow;
    private readonly CompositeDisposable _subscriptions = new();
    private ProjectOutputSnapshot _current = Empty;

    public ProjectOutputService(IProjectWorkflowService workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));

        _current = Map(_workflow.Current);
        _subscriptions.Add(
            _workflow.WhenChanged
                .Select(Map)
                .Subscribe(snapshot => _current = snapshot));
    }

    /// <inheritdoc />
    public ProjectOutputSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<ProjectOutputSnapshot> WhenChanged =>
        _workflow.WhenChanged.Select(Map);

    /// <inheritdoc />
    public void Dispose() => _subscriptions.Dispose();

    private static ProjectOutputSnapshot Map(ProjectWorkflowSnapshot snapshot) =>
        new(
            snapshot.Generation,
            snapshot.State,
            snapshot.ActiveOperation,
            snapshot.LastOutcome,
            snapshot.TargetFilePath,
            snapshot.OutputLines,
            snapshot.LastOperation);
}
