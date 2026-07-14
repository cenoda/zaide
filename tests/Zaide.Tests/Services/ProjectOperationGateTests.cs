using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M3a tests for the shared <see cref="ProjectOperationGate"/>.
/// </summary>
public sealed class ProjectOperationGateTests
{
    private sealed class MutableDebugSessionService : IDebugSessionService
    {
        private readonly Subject<DebugSessionSnapshot> _subject = new();
        private DebugSessionSnapshot _current = Idle();

        public DebugSessionSnapshot Current => _current;

        public IObservable<DebugSessionSnapshot> WhenChanged => _subject;

        public void SetState(DebugSessionState state)
        {
            _current = _current with { State = state };
            _subject.OnNext(_current);
        }

        public Task<DebugSessionOperationResult> StartLaunchAsync(
            DebugLaunchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> ReportPreLaunchFailureAsync(
            DebugSessionOutcomeKind kind,
            string message,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StopAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> ContinueAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> PauseAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StepOverAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StepIntoAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StepOutAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestThreadsAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestStackTraceAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestScopesAsync(
            int frameId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestVariablesAsync(
            int variablesReference,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> ReplaceBreakpointsBySourceAsync(
            System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<int>> replacementBySource,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }

        private static DebugSessionSnapshot Idle() => new(
            DebugSessionState.Idle,
            Generation: 0,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);
    }

    [Fact]
    public async Task WorkflowOperation_RejectsSecondWorkflowAdmission()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        var first = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        Assert.True(first.IsSuccess);

        var second = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Run);
        Assert.False(second.IsSuccess);
        Assert.Equal(ProjectOperationRejectionReason.WorkflowBusy, second.RejectionReason);
        Assert.Equal(ProjectOperationGateMessages.WorkflowBusy, second.Message);

        first.Lease!.Dispose();
    }

    [Fact]
    public async Task DebugHandoff_RejectsWorkflowWhileActive()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        var handoff = await gate.TryAcquireDebugHandoffAsync();
        Assert.True(handoff.IsSuccess);
        Assert.True(gate.IsDebugHandoffActive);

        var build = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        Assert.False(build.IsSuccess);
        Assert.Equal(ProjectOperationRejectionReason.DebugSessionActive, build.RejectionReason);
        Assert.Equal(ProjectOperationGateMessages.DebugSessionActive, build.Message);

        handoff.Lease!.Dispose();
        Assert.False(gate.IsDebugHandoffActive);
    }

    [Fact]
    public async Task DebugHandoff_RejectsConcurrentDebugStart()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        var first = await gate.TryAcquireDebugHandoffAsync();
        var second = await gate.TryAcquireDebugHandoffAsync();

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(ProjectOperationGateMessages.DebugSessionActive, second.Message);

        first.Lease!.Dispose();
    }

    [Fact]
    public async Task WorkflowOperation_RejectsDebugHandoffWhileActive()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        var workflow = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Test);
        Assert.True(workflow.IsSuccess);

        var handoff = await gate.TryAcquireDebugHandoffAsync();
        Assert.False(handoff.IsSuccess);
        Assert.Equal(ProjectOperationGateMessages.WorkflowBusy, handoff.Message);

        workflow.Lease!.Dispose();
    }

    [Fact]
    public async Task ActiveDebugSession_RejectsWorkflowAndDebugHandoff()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        debug.SetState(DebugSessionState.Running);

        var workflow = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        var handoff = await gate.TryAcquireDebugHandoffAsync();

        Assert.False(workflow.IsSuccess);
        Assert.False(handoff.IsSuccess);
        Assert.Equal(ProjectOperationGateMessages.DebugSessionActive, workflow.Message);
        Assert.Equal(ProjectOperationGateMessages.DebugSessionActive, handoff.Message);
    }

    [Fact]
    public async Task HandoffLease_ReleaseAfterDispose_AllowsWorkflow()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        var handoff = await gate.TryAcquireDebugHandoffAsync();
        handoff.Lease!.Dispose();

        var workflow = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        Assert.True(workflow.IsSuccess);
        workflow.Lease!.Dispose();
    }

    [Fact]
    public async Task Handoff_NoAdmissionGapBetweenBuildCompleteAndLaunch()
    {
        var debug = new MutableDebugSessionService();
        using var gate = new ProjectOperationGate(debug);

        var handoff = await gate.TryAcquireDebugHandoffAsync();
        Assert.True(handoff.IsSuccess);

        // Simulate post-build / pre-launch handoff window.
        var blocked = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        Assert.False(blocked.IsSuccess);
        Assert.Equal(ProjectOperationGateMessages.DebugSessionActive, blocked.Message);

        handoff.Lease!.Dispose();
    }
}