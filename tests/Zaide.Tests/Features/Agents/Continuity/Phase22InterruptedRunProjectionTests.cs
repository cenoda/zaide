using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Features.Agents.Continuity;

public sealed class Phase22InterruptedRunProjectionTests : IDisposable
{
    private readonly Phase22ContinuityTestSupport.Harness _nativeHarness;
    private readonly Phase22ContinuityTestSupport.Harness _acpHarness;

    public Phase22InterruptedRunProjectionTests()
    {
        _nativeHarness = new Phase22ContinuityTestSupport.Harness(AgentBackendIds.NativeHarness);
        _acpHarness = new Phase22ContinuityTestSupport.Harness(AgentBackendIds.Acp);
    }

    public void Dispose()
    {
        _nativeHarness.Dispose();
        _acpHarness.Dispose();
    }

    [Fact]
    public void RestartClassification_ProjectsExactlyOnce()
    {
        // Unit-level classification/projection only. Real force-quit process-group
        // evidence is owned by the out-of-tree M4 A3 producer under
        // /tmp/zaide-a3-agent-path/runner/ (not this in-process fixture).
        _nativeHarness.RecordInterruptedCheckpointAtWorkspaceRoot();

        var restartedCoordinator = Phase21ContinuityTestSupport.CreateCoordinator(
            _nativeHarness.Store,
            _nativeHarness.BindingStore);
        var restartedProjector = new AgentSessionContinuityConversationProjector(
            _nativeHarness.ConversationStore,
            _nativeHarness.Catalog);

        var summary = restartedCoordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            _nativeHarness.WorkspaceKey,
            _nativeHarness.WorkspaceRoot,
            origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));
        restartedProjector.ProjectReconcileSummary(
            summary,
            AgentSessionContinuityReconcileOrigin.WorkspaceOpen);
        restartedProjector.ProjectReconcileSummary(
            summary,
            AgentSessionContinuityReconcileOrigin.WorkspaceOpen);

        Assert.Equal(1, _nativeHarness.CountInterruptedProjectionEntries());
    }

    [Fact]
    public void InterruptedTownhallEntry_IsTerminalActionable_AndDoesNotClaimResume()
    {
        _nativeHarness.RecordInterruptedCheckpointAtWorkspaceRoot();
        _nativeHarness.WorkspaceOpenReconciler.ReconcileOnWorkspaceOpenIfNeeded();

        var entry = _nativeHarness.ConversationStore.TryGet(_nativeHarness.ConversationId, out var conversation)
            ? conversation.Entries.Single(e =>
                e.Content.StartsWith(
                    AgentConversationEventProjection.InterruptedRunContentPrefix,
                    StringComparison.Ordinal))
            : throw new InvalidOperationException("Missing interrupted projection.");

        Assert.Equal(ConversationEntryKind.ExecutionFailure, entry.Kind);
        var display = TownhallEntryProjection.ToTownhallDisplayContent(entry);
        Assert.Contains("Resume is not available", display, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Send your message again", display, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Interrupted run", display, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reconcile_DoesNotInvokeBackend_BeforeExplicitResend()
    {
        _nativeHarness.RecordInterruptedCheckpointAtWorkspaceRoot();
        _nativeHarness.WorkspaceOpenReconciler.ReconcileOnWorkspaceOpenIfNeeded();

        Assert.Equal(0, _nativeHarness.Backend.ExecuteCallCount);

        var runId = await _nativeHarness.SendExplicitResendAsync(_nativeHarness.SessionService);
        Assert.NotEqual(default, runId);
        Assert.Equal(1, _nativeHarness.Backend.ExecuteCallCount);
    }

    [Fact]
    public async Task ExplicitResend_CreatesNewSessionAndRun_WithoutResumeOrReplay()
    {
        var interruptedSessionId = _nativeHarness.RecordInterruptedCheckpointAtWorkspaceRoot();
        _nativeHarness.WorkspaceOpenReconciler.ReconcileOnWorkspaceOpenIfNeeded();

        var restartedSession = _nativeHarness.CreateSessionService(
            Phase21ContinuityTestSupport.CreateCoordinator(_nativeHarness.Store, _nativeHarness.BindingStore));
        using (restartedSession)
        {
            Assert.Null(restartedSession.TryGetSessionSnapshot(_nativeHarness.ConversationId));

            var runId = await _nativeHarness.SendExplicitResendAsync(restartedSession);
            var sessionSnapshot = restartedSession.TryGetSessionSnapshot(_nativeHarness.ConversationId)
                ?? throw new InvalidOperationException("Missing live session after explicit re-send.");

            Assert.NotEqual(interruptedSessionId, sessionSnapshot.SessionId);
            Assert.NotEqual(default, runId);
        }
    }

    [Theory]
    [InlineData("native-harness")]
    [InlineData("acp")]
    public void BackendResume_IsUnusable_ForBothSiblingBackends(string backendToken)
    {
        var backendId = backendToken == "acp"
            ? AgentBackendIds.Acp
            : AgentBackendIds.NativeHarness;
        var harness = backendToken == "acp" ? _acpHarness : _nativeHarness;

        var sessionId = harness.RecordInterruptedCheckpointAtWorkspaceRoot();
        var summary = harness.Coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            harness.WorkspaceKey,
            harness.WorkspaceRoot,
            origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));

        var interrupted = summary.InterruptedSessions.Single(item => item.Scope.SessionId == sessionId);
        var resume = harness.Coordinator.Resume(new AgentSessionContinuityResumeRequest(
            harness.WorkspaceKey,
            harness.WorkspaceRoot,
            harness.ConversationId,
            sessionId,
            harness.ActorId,
            backendId,
            idempotencyKey: $"resume-{backendToken}"));

        Assert.False(interrupted.ResumeAdmitted);
        Assert.Equal(AgentSessionContinuityOperationStatus.Indeterminate, resume.Status);
        Assert.False(harness.Coordinator.TryGetResumedSessionId(harness.ConversationId, out _));
    }

    [Fact]
    public void NoCrossBackendResumeFallbackOrRetry()
    {
        var sessionId = _nativeHarness.RecordInterruptedCheckpointAtWorkspaceRoot();
        Phase21ContinuityTestSupport.SeedBinding(_nativeHarness.BindingStore, _nativeHarness.ActorId, AgentBackendIds.Acp);

        var nativeResume = _nativeHarness.Coordinator.Resume(new AgentSessionContinuityResumeRequest(
            _nativeHarness.WorkspaceKey,
            _nativeHarness.WorkspaceRoot,
            _nativeHarness.ConversationId,
            sessionId,
            _nativeHarness.ActorId,
            AgentBackendIds.NativeHarness,
            idempotencyKey: "cross-native"));

        var acpResume = _nativeHarness.Coordinator.Resume(new AgentSessionContinuityResumeRequest(
            _nativeHarness.WorkspaceKey,
            _nativeHarness.WorkspaceRoot,
            _nativeHarness.ConversationId,
            sessionId,
            _nativeHarness.ActorId,
            AgentBackendIds.Acp,
            idempotencyKey: "cross-acp"));

        Assert.Equal(AgentSessionContinuityOperationStatus.Indeterminate, nativeResume.Status);
        Assert.Equal(AgentSessionContinuityOperationStatus.Rejected, acpResume.Status);
    }
}
