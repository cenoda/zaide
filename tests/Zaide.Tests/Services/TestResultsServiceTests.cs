using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M5 tests for <see cref="TestResultsService"/> lifecycle policy.
/// </summary>
public sealed class TestResultsServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-test-results-" + Guid.NewGuid().ToString("N"));

    static TestResultsServiceTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class FakeWorkflowService : IProjectWorkflowService
    {
        private readonly BehaviorSubject<ProjectWorkflowSnapshot> _subject;
        private ProjectWorkflowSnapshot _current;

        public FakeWorkflowService(ProjectWorkflowSnapshot initial)
        {
            _current = initial;
            _subject = new BehaviorSubject<ProjectWorkflowSnapshot>(initial);
        }

        public ProjectWorkflowSnapshot Current => _current;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _subject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived =>
            new Subject<ManagedProcessOutputLine>();

        public void Publish(ProjectWorkflowSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    private static ProjectWorkflowSnapshot Idle(long generation = 0) => new(
        ProjectWorkflowOperationState.Idle,
        generation,
        ActiveOperation: null,
        LastOutcome: null,
        TargetFilePath: null,
        ProcessId: null,
        OutputLines: Array.Empty<ManagedProcessOutputLine>());

    [Fact]
    public void TestStart_ClearsPreviousResults()
    {
        var target = Path.Combine(TempRoot, "Pass.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            1,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            1,
            null,
            ProjectWorkflowOutcomeKind.Succeeded,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(
                    1,
                    ProcessStreamKind.StdOut,
                    "Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 1 ms",
                    DateTimeOffset.UtcNow),
            }));

        Assert.NotNull(service.Current.Summary);
        Assert.Equal(1, service.Current.Summary!.Passed);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            2,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        Assert.Empty(service.Current.Cases);
        Assert.Null(service.Current.Summary);
        Assert.Equal(2, service.Current.Generation);
    }

    [Fact]
    public void TestComplete_UnparsableOutput_IsPartialWithNoInventedPasses()
    {
        var target = Path.Combine(TempRoot, "Opaque.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            4,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            4,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(
                    1,
                    ProcessStreamKind.StdOut,
                    "mystery output with no parseable test lines",
                    DateTimeOffset.UtcNow),
            }));

        Assert.Empty(service.Current.Cases);
        Assert.Null(service.Current.Summary);
        Assert.True(service.Current.IsPartial);
        Assert.Equal(ProjectWorkflowOutcomeKind.Failed, service.Current.OperationOutcome);
    }

    [Fact]
    public void TestCancelled_MarksPartialSnapshot()
    {
        var target = Path.Combine(TempRoot, "Cancel.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            5,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            5,
            null,
            ProjectWorkflowOutcomeKind.Cancelled,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        Assert.True(service.Current.IsPartial);
        Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, service.Current.OperationOutcome);
    }

    [Fact]
    public void TestComplete_SummaryOnlyPass_IsNotPartial()
    {
        var target = Path.Combine(TempRoot, "Pass.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            8,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            8,
            null,
            ProjectWorkflowOutcomeKind.Succeeded,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(
                    1,
                    ProcessStreamKind.StdOut,
                    "Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 1 ms",
                    DateTimeOffset.UtcNow),
            }));

        Assert.False(service.Current.IsPartial);
        Assert.Equal(3, service.Current.Summary!.Passed);
        Assert.Empty(service.Current.Cases);
    }

    [Fact]
    public void TestComplete_FailSummaryWithoutParsedCases_IsPartial()
    {
        var target = Path.Combine(TempRoot, "FailSummaryOnly.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            9,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            9,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(
                    1,
                    ProcessStreamKind.StdOut,
                    "Failed!  - Failed:     2, Passed:     0, Skipped:     0, Total:     2, Duration: 1 ms",
                    DateTimeOffset.UtcNow),
            }));

        Assert.True(service.Current.IsPartial);
        Assert.Equal(2, service.Current.Summary!.Failed);
        Assert.Empty(service.Current.Cases);
    }

    [Fact]
    public void TestComplete_VstestFormat_IsNotPartialWithSummary()
    {
        var target = Path.Combine(TempRoot, "Vstest.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            10,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            10,
            null,
            ProjectWorkflowOutcomeKind.Succeeded,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(1, ProcessStreamKind.StdOut, "Test Run Successful.", DateTimeOffset.UtcNow),
                new ManagedProcessOutputLine(2, ProcessStreamKind.StdOut, "Total tests: 2", DateTimeOffset.UtcNow),
                new ManagedProcessOutputLine(3, ProcessStreamKind.StdOut, "     Passed: 2", DateTimeOffset.UtcNow),
            }));

        Assert.False(service.Current.IsPartial);
        Assert.Equal(2, service.Current.Summary!.Passed);
    }

    [Fact]
    public void StaleGeneration_IsIgnored()
    {
        var target = Path.Combine(TempRoot, "Stale.csproj");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new TestResultsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            6,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            7,
            ProjectWorkflowOperation.Test,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            6,
            null,
            ProjectWorkflowOutcomeKind.Succeeded,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(
                    1,
                    ProcessStreamKind.StdOut,
                    "Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9, Duration: 1 ms",
                    DateTimeOffset.UtcNow),
            }));

        Assert.Null(service.Current.Summary);
        Assert.Equal(7, service.Current.Generation);
    }
}
