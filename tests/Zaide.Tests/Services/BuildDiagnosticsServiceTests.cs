using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M3 tests for <see cref="BuildDiagnosticsService"/> lifecycle policy.
/// </summary>
public sealed class BuildDiagnosticsServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-build-diags-" + Guid.NewGuid().ToString("N"));

    static BuildDiagnosticsServiceTests()
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

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

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
    public void BuildStart_ClearsPreviousBuildDiagnosticsOnly()
    {
        var target = Path.Combine(TempRoot, "App.csproj");
        var file = Path.Combine(TempRoot, "App.cs");
        var errorLine = $"{file}(1,2): error CS1002: ; expected";

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new BuildDiagnosticsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            1,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            1,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(1, ProcessStreamKind.StdErr, errorLine, DateTimeOffset.UtcNow),
            }));

        Assert.Single(service.Current.Diagnostics);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            2,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        Assert.Empty(service.Current.Diagnostics);
        Assert.Equal(2, service.Current.BuildGeneration);
    }

    [Fact]
    public void BuildComplete_SetsDiagnosticsFromParsedOutput()
    {
        var target = Path.Combine(TempRoot, "Complete.csproj");
        var file = Path.Combine(TempRoot, "Complete.cs");
        var errorLine = $"{file}(4,5): error CS0103: missing name";

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new BuildDiagnosticsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            3,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            3,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(3, ProcessStreamKind.StdErr, errorLine, DateTimeOffset.UtcNow),
            }));

        var diagnostic = Assert.Single(service.Current.Diagnostics);
        Assert.Equal(ProjectWorkflowOutcomeKind.Failed, service.Current.LastOutcome);
        Assert.False(service.Current.IsPartial);
        Assert.Equal("CS0103", diagnostic.Code);
    }

    [Fact]
    public void BuildCancelled_KeepsPartialDiagnostics()
    {
        var target = Path.Combine(TempRoot, "Cancel.csproj");
        var file = Path.Combine(TempRoot, "Cancel.cs");
        var errorLine = $"{file}(2,3): error CS1002: ; expected";

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new BuildDiagnosticsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            4,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            4,
            null,
            ProjectWorkflowOutcomeKind.Cancelled,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(4, ProcessStreamKind.StdErr, errorLine, DateTimeOffset.UtcNow),
            }));

        Assert.Single(service.Current.Diagnostics);
        Assert.True(service.Current.IsPartial);
        Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, service.Current.LastOutcome);
    }

    [Fact]
    public void StaleGenerationTerminalOutcome_IsIgnored()
    {
        var target = Path.Combine(TempRoot, "Stale.csproj");
        var file = Path.Combine(TempRoot, "Stale.cs");
        var errorLine = $"{file}(1,1): error CS1002: ; expected";

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new BuildDiagnosticsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            5,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            6,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            5,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(5, ProcessStreamKind.StdErr, errorLine, DateTimeOffset.UtcNow),
            }));

        Assert.Empty(service.Current.Diagnostics);
        Assert.Equal(6, service.Current.BuildGeneration);
    }

    [Fact]
    public void RunTerminalOutcome_DoesNotTouchBuildDiagnostics()
    {
        var target = Path.Combine(TempRoot, "Run.csproj");
        var file = Path.Combine(TempRoot, "Run.cs");
        var errorLine = $"{file}(1,1): error CS1002: ; expected";

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new BuildDiagnosticsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            7,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(7, ProcessStreamKind.StdErr, errorLine, DateTimeOffset.UtcNow),
            }));

        Assert.Empty(service.Current.Diagnostics);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            8,
            ProjectWorkflowOperation.Run,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            8,
            null,
            ProjectWorkflowOutcomeKind.Failed,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(8, ProcessStreamKind.StdErr, errorLine, DateTimeOffset.UtcNow),
            }));

        Assert.Empty(service.Current.Diagnostics);
    }

    [Fact]
    public void BuildComplete_ParsesDoneAndMessageSeverities()
    {
        var target = Path.Combine(TempRoot, "SevFlow.csproj");
        var file = Path.Combine(TempRoot, "SevFlow.cs");

        using var workflow = new FakeWorkflowService(Idle());
        using var service = new BuildDiagnosticsService(workflow);

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            20,
            ProjectWorkflowOperation.Build,
            null,
            target,
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        workflow.Publish(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            20,
            null,
            ProjectWorkflowOutcomeKind.Succeeded,
            target,
            null,
            new[]
            {
                new ManagedProcessOutputLine(1, ProcessStreamKind.StdOut,
                    $"{file}(1,1): done build completed successfully", DateTimeOffset.UtcNow),
                new ManagedProcessOutputLine(2, ProcessStreamKind.StdOut,
                    $"{file}(2,1): message MSB3123: some hint", DateTimeOffset.UtcNow),
            }));

        Assert.Equal(2, service.Current.Diagnostics.Count);
        Assert.Equal(LanguageDiagnosticSeverity.Information, service.Current.Diagnostics[0].Severity);
        Assert.Equal(LanguageDiagnosticSeverity.Hint, service.Current.Diagnostics[1].Severity);
        Assert.False(service.Current.IsPartial);
    }
}
