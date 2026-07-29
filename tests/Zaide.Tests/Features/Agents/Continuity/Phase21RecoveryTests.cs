using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Continuity;

public sealed class Phase21RecoveryTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _workspaceRoot;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;

    public Phase21RecoveryTests()
    {
        (_rootDirectory, _workspaceRoot, _workspaceKey) =
            Phase21ContinuityTestSupport.CreateWorkspaceFixture();
    }

    public void Dispose() => Phase21ContinuityTestSupport.DeleteDirectory(_rootDirectory);

    [Fact]
    public void Resume_ExplicitUserAction_RevalidatesIdentityAndRecordsCheckpoint()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-alpha");
        var backendId = AgentBackendIds.NativeHarness;
        Phase21ContinuityTestSupport.SeedBinding(bindingStore, actorId, backendId);

        var conversationId = ConversationId.NewDirect();
        var sessionId = AgentSessionId.New();
        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        var checkpoint = Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId);
        coordinator.RecordCheckpoint(checkpoint);

        var result = coordinator.Resume(new AgentSessionContinuityResumeRequest(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId,
            idempotencyKey: "resume-1"));

        Assert.Equal(AgentSessionContinuityOperationStatus.Accepted, result.Status);
        Assert.Equal(AgentSessionContinuityOperationKind.Resume, result.Operation);
        Assert.True(coordinator.TryGetResumedSessionId(conversationId, out var resumedId));
        Assert.Equal(sessionId, resumedId);
    }

    [Fact]
    public void Resume_IsIdempotent_ForSameIdempotencyKey()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-beta");
        var backendId = AgentBackendIds.NativeHarness;
        Phase21ContinuityTestSupport.SeedBinding(bindingStore, actorId, backendId);

        var conversationId = ConversationId.NewDirect();
        var sessionId = AgentSessionId.New();
        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        coordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId));

        var request = new AgentSessionContinuityResumeRequest(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId,
            idempotencyKey: "resume-dup");

        var first = coordinator.Resume(request);
        var second = coordinator.Resume(request);

        Assert.Equal(AgentSessionContinuityOperationStatus.Accepted, first.Status);
        Assert.Equal(AgentSessionContinuityOperationStatus.DuplicateIgnored, second.Status);
    }

    [Fact]
    public void Resume_RejectsIdentityMismatch()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-gamma");
        Phase21ContinuityTestSupport.SeedBinding(bindingStore, actorId, AgentBackendIds.NativeHarness);

        var conversationId = ConversationId.NewDirect();
        var sessionId = AgentSessionId.New();
        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        coordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            AgentBackendIds.NativeHarness));

        var result = coordinator.Resume(new AgentSessionContinuityResumeRequest(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            ActorId.PanelSeed("other-actor"),
            AgentBackendIds.NativeHarness,
            idempotencyKey: "resume-mismatch"));

        Assert.Equal(AgentSessionContinuityOperationStatus.Rejected, result.Status);
    }

    [Fact]
    public void Reconcile_DoesNotAutoResumeSideEffectingWork()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-delta");
        Phase21ContinuityTestSupport.SeedBinding(bindingStore, actorId, AgentBackendIds.NativeHarness);

        var conversationId = ConversationId.NewDirect();
        var sessionId = AgentSessionId.New();
        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store, bindingStore);
        coordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            AgentBackendIds.NativeHarness,
            AgentRunStatus.Running));

        var summary = coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            _workspaceKey,
            _workspaceRoot,
            isStartup: true));

        Assert.True(summary.RecoverableCount >= 1);
        Assert.False(coordinator.TryGetResumedSessionId(conversationId, out _));
    }
}
