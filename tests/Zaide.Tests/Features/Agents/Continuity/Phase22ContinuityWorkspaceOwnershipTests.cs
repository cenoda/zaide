using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Features.Agents.Continuity;

public sealed class Phase22ContinuityWorkspaceOwnershipTests : IDisposable
{
    private readonly Phase22ContinuityTestSupport.Harness _harness;

    public Phase22ContinuityWorkspaceOwnershipTests()
    {
        _harness = new Phase22ContinuityTestSupport.Harness(AgentBackendIds.NativeHarness);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void LiveAdmittedWork_WritesCheckpointUnderOpenedWorkspaceRoot()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_harness.ProcessCwd);
            _harness.RecordInterruptedCheckpointAtWorkspaceRoot();

            var replay = _harness.Store.Replay(new AgentDurableRecordReplayRequest(
                _harness.WorkspaceKey,
                AgentDurableRecordClass.SessionRecovery,
                afterOrderingSequence: 0,
                maxRecords: 32));

            Assert.Contains(
                replay.Records,
                record => record.PayloadJson.Contains(_harness.WorkspaceRoot, StringComparison.Ordinal));
            Assert.DoesNotContain(
                replay.Records,
                record => record.PayloadJson.Contains(_harness.ProcessCwd, StringComparison.Ordinal)
                    && !record.PayloadJson.Contains(_harness.WorkspaceRoot, StringComparison.Ordinal));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void ProcessCwdDiffersFromWorkspaceRoot_WithoutChangingCheckpointOwnership()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_harness.ProcessCwd);
            var sessionId = _harness.RecordInterruptedCheckpointAtWorkspaceRoot();

            var workspaceSummary = _harness.Coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
                _harness.WorkspaceKey,
                _harness.WorkspaceRoot,
                origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));
            var cwdSummary = _harness.LegacyCwdReader.ReadLegacyCwdInterruptedSessions();

            Assert.Contains(workspaceSummary.InterruptedSessions, item => item.Scope.SessionId == sessionId);
            Assert.Empty(cwdSummary.InterruptedSessions);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void LegacyCwdRecord_IsReadOnlyLabelledLegacy_NotMergedOrDeleted()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_harness.ProcessCwd);

            var legacySessionId = _harness.RecordLegacyCwdCheckpoint();
            var legacyRecordCountBefore = _harness.Store.Replay(new AgentDurableRecordReplayRequest(
                _harness.LegacyCwdKey,
                AgentDurableRecordClass.SessionRecovery,
                afterOrderingSequence: 0,
                maxRecords: 32)).Records.Count;

            var workspaceSessionId = _harness.RecordInterruptedCheckpointAtWorkspaceRoot();

            var legacySummary = _harness.LegacyCwdReader.ReadLegacyCwdInterruptedSessions();
            var startupSummary = _harness.StartupReconciler.ReconcileOnStartupIfNeeded();

            var legacyRecordCountAfter = _harness.Store.Replay(new AgentDurableRecordReplayRequest(
                _harness.LegacyCwdKey,
                AgentDurableRecordClass.SessionRecovery,
                afterOrderingSequence: 0,
                maxRecords: 32)).Records.Count;

            Assert.True(legacyRecordCountAfter >= legacyRecordCountBefore);
            Assert.Contains(legacySummary.InterruptedSessions, item => item.Scope.SessionId == legacySessionId);
            Assert.Contains(startupSummary.InterruptedSessions, item => item.Scope.SessionId == legacySessionId);
            Assert.DoesNotContain(startupSummary.InterruptedSessions, item => item.Scope.SessionId == workspaceSessionId);

            Assert.Equal(1, _harness.CountInterruptedProjectionEntries());
            var entry = _harness.ConversationStore.TryGet(_harness.ConversationId, out var conversation)
                ? conversation.Entries.Single(e =>
                    e.Content.StartsWith(
                        AgentConversationEventProjection.InterruptedRunContentPrefix,
                        StringComparison.Ordinal))
                : throw new InvalidOperationException("Missing projected entry.");
            Assert.Contains("legacy-cwd", entry.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void WorkspaceOpenReconciliation_IsDistinctFromStartup_AndIdempotent()
    {
        _harness.RecordInterruptedCheckpointAtWorkspaceRoot();

        var first = _harness.WorkspaceOpenReconciler.ReconcileOnWorkspaceOpenIfNeeded();
        var second = _harness.WorkspaceOpenReconciler.ReconcileOnWorkspaceOpenIfNeeded();

        Assert.True(first.RecoverableCount >= 1);
        Assert.Equal(0, second.RecoverableCount);
        Assert.Equal(1, _harness.CountInterruptedProjectionEntries());

        var startup = _harness.StartupReconciler.ReconcileOnStartupIfNeeded();
        Assert.Equal(0, startup.RecoverableCount);
    }

    [Fact]
    public void BindingMismatch_RemainsFailClosedAndClassified()
    {
        var sessionId = _harness.RecordInterruptedCheckpointAtWorkspaceRoot();
        Phase21ContinuityTestSupport.SeedBinding(_harness.BindingStore, _harness.ActorId, AgentBackendIds.Acp);

        var summary = _harness.Coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            _harness.WorkspaceKey,
            _harness.WorkspaceRoot,
            origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));

        var interrupted = summary.InterruptedSessions.Single(item => item.Scope.SessionId == sessionId);
        Assert.Equal(AgentSessionContinuityClassification.Indeterminate, interrupted.Classification);
    }

    [Fact]
    public void WorkspaceMismatch_RemainsFailClosedAndClassified()
    {
        var sessionId = _harness.RecordInterruptedCheckpointAtWorkspaceRoot();
        var otherRoot = Path.Combine(Path.GetTempPath(), "other-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(otherRoot);
        try
        {
            var otherKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(otherRoot);
            var summary = _harness.Coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
                otherKey,
                otherRoot,
                origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));

            Assert.DoesNotContain(summary.InterruptedSessions, item => item.Scope.SessionId == sessionId);
        }
        finally
        {
            Directory.Delete(otherRoot, recursive: true);
        }
    }

    [Fact]
    public void NativeHarnessAndAcpCapabilityMatrix_BothReportResumeUnusable()
    {
        var rows = AgentBackendContinuityCapabilityMatrix.Rows;
        Assert.All(rows, row => Assert.False(row.ResumeCurrentlyUsable));
        Assert.Contains(rows, row => row.BackendId == AgentBackendIds.NativeHarnessValue);
        Assert.Contains(rows, row => row.BackendId == AgentBackendIds.AcpValue);
    }
}
