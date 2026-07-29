using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Continuity;

public sealed class Phase21RestartTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _workspaceRoot;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;

    public Phase21RestartTests()
    {
        (_rootDirectory, _workspaceRoot, _workspaceKey) =
            Phase21ContinuityTestSupport.CreateWorkspaceFixture();
    }

    public void Dispose() => Phase21ContinuityTestSupport.DeleteDirectory(_rootDirectory);

    [Fact]
    public void Restart_ReconcileClassifiesInterruptedSessionWithoutLiveSession()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-restart");
        Phase21ContinuityTestSupport.SeedBinding(bindingStore, actorId, AgentBackendIds.NativeHarness);

        var conversationId = ConversationId.NewDirect();
        var sessionId = AgentSessionId.New();
        var writerCoordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        writerCoordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            AgentBackendIds.NativeHarness,
            AgentRunStatus.Running));

        var restartedCoordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        var summary = restartedCoordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            _workspaceKey,
            _workspaceRoot,
            isStartup: true));

        Assert.True(summary.RecoverableCount >= 1);
        Assert.Contains(
            summary.InterruptedSessions,
            item => item.Scope.SessionId == sessionId);
    }

    [Fact]
    public void StartupReconciler_IsIdempotent()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-startup");
        Phase21ContinuityTestSupport.SeedBinding(bindingStore, actorId, AgentBackendIds.NativeHarness);

        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        coordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
            _workspaceKey,
            _workspaceRoot,
            ConversationId.NewDirect(),
            AgentSessionId.New(),
            actorId,
            AgentBackendIds.NativeHarness));

        var resolver = new PathDerivedAgentDurableWorkspaceStorageKeyResolver();
        var startup = new AgentSessionContinuityStartupReconciler(
            coordinator,
            resolver,
            () => _workspaceRoot);

        var first = startup.ReconcileOnStartupIfNeeded();
        var second = startup.ReconcileOnStartupIfNeeded();

        Assert.True(first.RecoverableCount >= 1);
        Assert.Equal(0, second.RecoverableCount);
    }

    [Fact]
    public void DisconnectAndLateCompletion_RemainRepresentableInCheckpoint()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store);
        var actorId = ActorId.PanelSeed("agent-evidence");
        var scope = new AgentSessionContinuityScope(
            actorId,
            ConversationId.NewDirect(),
            AgentSessionId.New(),
            ExecutionRunId.New(),
            AgentBackendIds.Acp,
            _workspaceKey,
            _workspaceRoot);

        var checkpoint = new AgentSessionContinuityCheckpoint(
            AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
            scope,
            AgentSessionContinuityClassification.Indeterminate,
            AgentSessionStatus.Ready,
            AgentRunStatus.Disconnected,
            AgentSessionContinuityLimits.PayloadSchemaVersion,
            AgentSessionContinuityBindingFingerprint.Compute(
                actorId,
                AgentBackendIds.Acp,
                _workspaceRoot),
            capabilitySnapshotVersion: 1,
            DateTimeOffset.UtcNow,
            disconnectEvidence: "backend-disconnect-observed",
            lateCompletionEvidence: "late-completion-observed");

        coordinator.RecordCheckpoint(checkpoint);
        var summary = coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            _workspaceKey,
            _workspaceRoot,
            isStartup: true));

        Assert.True(summary.IndeterminateCount >= 1);
    }

    [Fact]
    public void BackendCapabilityMatrix_ReportsSiblingBackendsIndependently()
    {
        var rows = AgentBackendContinuityCapabilityMatrix.Rows;
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.BackendId == AgentBackendIds.NativeHarnessValue);
        Assert.Contains(rows, row => row.BackendId == AgentBackendIds.AcpValue);
        Assert.All(rows, row => Assert.False(row.ResumeCurrentlyUsable));
    }
}
