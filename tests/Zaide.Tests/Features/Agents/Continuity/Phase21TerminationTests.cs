using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Continuity;

public sealed class Phase21TerminationTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _workspaceRoot;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;

    public Phase21TerminationTests()
    {
        (_rootDirectory, _workspaceRoot, _workspaceKey) =
            Phase21ContinuityTestSupport.CreateWorkspaceFixture();
    }

    public void Dispose() => Phase21ContinuityTestSupport.DeleteDirectory(_rootDirectory);

    [Fact]
    public void Terminate_RecordsLocalIntentSeparatelyFromBackendAcknowledgement()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-term");
        var backendId = AgentBackendIds.Acp;
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

        var result = coordinator.Terminate(new AgentSessionContinuityTerminateRequest(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId,
            idempotencyKey: "terminate-1"));

        Assert.Equal(AgentSessionContinuityOperationStatus.Accepted, result.Status);
        Assert.Equal(AgentSessionContinuityClassification.Terminal, result.Classification);
        Assert.Equal(
            AgentSessionContinuityAcknowledgementState.BackendAcknowledgementUnavailable,
            result.AcknowledgementState);
    }

    [Fact]
    public void Abandon_IsDistinctFromTerminate()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-abandon");
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

        var result = coordinator.Terminate(new AgentSessionContinuityTerminateRequest(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId,
            idempotencyKey: "abandon-1",
            terminationKind: AgentSessionContinuityOperationKind.Abandon));

        Assert.Equal(AgentSessionContinuityOperationKind.Abandon, result.Operation);
        Assert.Equal(AgentSessionContinuityClassification.Terminal, result.Classification);
    }

    [Fact]
    public void Terminate_IsIdempotent_ForSameIdempotencyKey()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.PanelSeed("agent-term-dup");
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

        var request = new AgentSessionContinuityTerminateRequest(
            _workspaceKey,
            _workspaceRoot,
            conversationId,
            sessionId,
            actorId,
            backendId,
            idempotencyKey: "terminate-dup");

        Assert.Equal(
            AgentSessionContinuityOperationStatus.Accepted,
            coordinator.Terminate(request).Status);
        Assert.Equal(
            AgentSessionContinuityOperationStatus.DuplicateIgnored,
            coordinator.Terminate(request).Status);
    }

    [Fact]
    public void Terminate_DoesNotClaimProviderDeletionWithoutEvidence()
    {
        var store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
        var coordinator = Phase21ContinuityTestSupport.CreateCoordinator(store);
        var result = coordinator.Terminate(new AgentSessionContinuityTerminateRequest(
            _workspaceKey,
            _workspaceRoot,
            ConversationId.NewDirect(),
            AgentSessionId.New(),
            ActorId.PanelSeed("agent-no-claim"),
            AgentBackendIds.Acp,
            idempotencyKey: "terminate-no-claim"));

        Assert.NotEqual(
            AgentSessionContinuityAcknowledgementState.BackendAcknowledged,
            result.AcknowledgementState);
    }
}
