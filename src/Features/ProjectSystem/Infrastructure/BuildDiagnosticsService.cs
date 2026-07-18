using System;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Subscribes to <see cref="IProjectWorkflowService"/> and owns build diagnostics
/// snapshots parsed at build terminal outcomes.
/// </summary>
internal sealed class BuildDiagnosticsService : IBuildDiagnosticsService
{
    private readonly IProjectWorkflowService _workflow;
    private readonly Subject<BuildDiagnosticsSnapshot> _subject = new();
    private readonly IDisposable _workflowSubscription;

    private BuildDiagnosticsSnapshot _current = BuildDiagnosticsSnapshot.Empty;
    private long _pendingBuildGeneration;
    private bool _disposed;

    public BuildDiagnosticsService(IProjectWorkflowService workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _workflowSubscription = _workflow.WhenChanged.Subscribe(OnWorkflowChanged);
    }

    /// <inheritdoc />
    public BuildDiagnosticsSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<BuildDiagnosticsSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _workflowSubscription.Dispose();
        _current = BuildDiagnosticsSnapshot.Empty;
        _subject.OnCompleted();
        _subject.Dispose();
    }

    private void OnWorkflowChanged(ProjectWorkflowSnapshot snapshot)
    {
        if (_disposed)
            return;

        if (snapshot.State == ProjectWorkflowOperationState.Starting &&
            snapshot.ActiveOperation == ProjectWorkflowOperation.Build)
        {
            if (snapshot.Generation < _pendingBuildGeneration)
                return;

            _pendingBuildGeneration = snapshot.Generation;
            Publish(new BuildDiagnosticsSnapshot(
                snapshot.Generation,
                LastOutcome: null,
                IsPartial: false,
                Array.Empty<BuildDiagnostic>()));
            return;
        }

        if (snapshot.State != ProjectWorkflowOperationState.Idle ||
            snapshot.LastOutcome is null ||
            snapshot.Generation != _pendingBuildGeneration)
        {
            return;
        }

        _pendingBuildGeneration = 0;

        var workingDirectory = string.IsNullOrWhiteSpace(snapshot.TargetFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(snapshot.TargetFilePath) ?? Environment.CurrentDirectory;

        var lines = snapshot.OutputLines.Select(line => line.Text);
        var diagnostics = BuildDiagnosticParser.Parse(lines, workingDirectory);
        var isPartial = snapshot.LastOutcome == ProjectWorkflowOutcomeKind.Cancelled;

        Publish(new BuildDiagnosticsSnapshot(
            snapshot.Generation,
            snapshot.LastOutcome,
            isPartial,
            diagnostics));
    }

    private void Publish(BuildDiagnosticsSnapshot snapshot)
    {
        _current = snapshot;
        _subject.OnNext(snapshot);
    }
}
