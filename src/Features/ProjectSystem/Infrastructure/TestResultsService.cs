using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Subscribes to <see cref="IProjectWorkflowService"/> and owns test-results
/// snapshots parsed at test terminal outcomes.
/// </summary>
public sealed class TestResultsService : ITestResultsService
{
    private readonly IProjectWorkflowService _workflow;
    private readonly Subject<TestResultsSnapshot> _subject = new();
    private readonly IDisposable _workflowSubscription;

    private TestResultsSnapshot _current = TestResultsSnapshot.Empty;
    private long _pendingTestGeneration;
    private bool _disposed;

    public TestResultsService(IProjectWorkflowService workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _workflowSubscription = _workflow.WhenChanged.Subscribe(OnWorkflowChanged);
    }

    /// <inheritdoc />
    public TestResultsSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<TestResultsSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _workflowSubscription.Dispose();
        _current = TestResultsSnapshot.Empty;
        _subject.OnCompleted();
        _subject.Dispose();
    }

    private void OnWorkflowChanged(ProjectWorkflowSnapshot snapshot)
    {
        if (_disposed)
            return;

        if (snapshot.State == ProjectWorkflowOperationState.Starting &&
            snapshot.ActiveOperation == ProjectWorkflowOperation.Test)
        {
            if (snapshot.Generation < _pendingTestGeneration)
                return;

            _pendingTestGeneration = snapshot.Generation;
            Publish(new TestResultsSnapshot(
                snapshot.Generation,
                OperationOutcome: null,
                IsPartial: false,
                Summary: null,
                Array.Empty<TestCaseResult>()));
            return;
        }

        if (snapshot.State != ProjectWorkflowOperationState.Idle ||
            snapshot.LastOutcome is null ||
            snapshot.Generation != _pendingTestGeneration ||
            snapshot.ActiveOperation is not null)
        {
            return;
        }

        _pendingTestGeneration = 0;

        var workingDirectory = string.IsNullOrWhiteSpace(snapshot.TargetFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(snapshot.TargetFilePath) ?? Environment.CurrentDirectory;

        var lines = snapshot.OutputLines.Select(line => line.Text);
        var (cases, summary, parseComplete) = TestResultsParser.Parse(lines, workingDirectory);
        var structurallyComplete = parseComplete && IsStructurallyComplete(summary, cases);
        var isPartial = snapshot.LastOutcome == ProjectWorkflowOutcomeKind.Cancelled || !structurallyComplete;

        Publish(new TestResultsSnapshot(
            snapshot.Generation,
            snapshot.LastOutcome,
            isPartial,
            summary,
            cases));
    }

    private void Publish(TestResultsSnapshot snapshot)
    {
        _current = snapshot;
        _subject.OnNext(snapshot);
    }

    private static bool IsStructurallyComplete(TestResultsSummary? summary, IReadOnlyList<TestCaseResult> cases)
    {
        if (summary?.Failed is not int failedCount || failedCount <= 0)
            return true;

        var parsedFailed = cases.Count(c => c.Outcome == TestCaseOutcome.Failed);
        return parsedFailed >= failedCount;
    }
}
