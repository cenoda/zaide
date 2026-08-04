using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            var beforeSnapshot = SnapshotLegacyPartition(_harness);

            var workspaceSessionId = _harness.RecordInterruptedCheckpointAtWorkspaceRoot();

            var legacySummary = _harness.LegacyCwdReader.ReadLegacyCwdInterruptedSessions();
            var startupSummary = _harness.StartupReconciler.ReconcileOnStartupIfNeeded();

            var afterSnapshot = SnapshotLegacyPartition(_harness);

            // Byte-for-byte equality: startup compatibility must not mutate legacy partition.
            Assert.Equal(beforeSnapshot.RecordCount, afterSnapshot.RecordCount);
            Assert.Equal(beforeSnapshot.OrderingSequences, afterSnapshot.OrderingSequences);
            Assert.Equal(beforeSnapshot.OperationIds, afterSnapshot.OperationIds);
            Assert.Equal(beforeSnapshot.Payloads, afterSnapshot.Payloads);
            Assert.Equal(beforeSnapshot.SerializedBytesSha256, afterSnapshot.SerializedBytesSha256);

            Assert.Contains(legacySummary.InterruptedSessions, item => item.Scope.SessionId == legacySessionId);
            Assert.Contains(startupSummary.InterruptedSessions, item => item.Scope.SessionId == legacySessionId);
            Assert.DoesNotContain(startupSummary.InterruptedSessions, item => item.Scope.SessionId == workspaceSessionId);

            // Workspace-owned records remain absent from legacy result.
            Assert.DoesNotContain(
                legacySummary.InterruptedSessions,
                item => item.Scope.SessionId == workspaceSessionId);

            Assert.Equal(1, _harness.CountInterruptedProjectionEntries());
            var legacyEntry = _harness.ConversationStore.TryGet(_harness.ConversationId, out var conversation)
                ? conversation.Entries.Single(e =>
                    e.Content.StartsWith(
                        AgentConversationEventProjection.InterruptedRunContentPrefix,
                        StringComparison.Ordinal))
                : throw new InvalidOperationException("Missing projected entry.");
            Assert.Contains("legacy-cwd", legacyEntry.Content, StringComparison.Ordinal);
            Assert.Contains("application-start legacy reconciliation", legacyEntry.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("workspace-owned", legacyEntry.Content, StringComparison.Ordinal);

            // Legacy records remain absent from workspace-open reconciliation;
            // do not copy the legacy record into the workspace partition.
            var workspaceSummary = _harness.WorkspaceOpenReconciler.ReconcileOnWorkspaceOpenIfNeeded();
            Assert.DoesNotContain(
                workspaceSummary.InterruptedSessions,
                item => item.Scope.SessionId == legacySessionId);
            Assert.Contains(
                workspaceSummary.InterruptedSessions,
                item => item.Scope.SessionId == workspaceSessionId);

            var afterWorkspaceOpen = SnapshotLegacyPartition(_harness);
            Assert.Equal(beforeSnapshot.SerializedBytesSha256, afterWorkspaceOpen.SerializedBytesSha256);
            Assert.Equal(2, _harness.CountInterruptedProjectionEntries());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void StartupLegacyCompatibility_PartitionBytesUnchanged_ExactEquality()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_harness.ProcessCwd);
            _harness.RecordLegacyCwdCheckpoint();
            var before = SnapshotLegacyPartition(_harness);

            var summary = _harness.StartupReconciler.ReconcileOnStartupIfNeeded();

            var after = SnapshotLegacyPartition(_harness);
            Assert.True(summary.InterruptedSessions.Count >= 1);
            Assert.Equal(before.RecordCount, after.RecordCount);
            Assert.Equal(before.OrderingSequences, after.OrderingSequences);
            Assert.Equal(before.OperationIds, after.OperationIds);
            Assert.Equal(before.Payloads, after.Payloads);
            Assert.Equal(before.SerializedBytesSha256, after.SerializedBytesSha256);
            Assert.Equal(before.FilePathsAndSizes, after.FilePathsAndSizes);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    private static LegacyPartitionSnapshot SnapshotLegacyPartition(Phase22ContinuityTestSupport.Harness harness)
    {
        var replay = harness.Store.Replay(new AgentDurableRecordReplayRequest(
            harness.LegacyCwdKey,
            AgentDurableRecordClass.SessionRecovery,
            afterOrderingSequence: 0,
            maxRecords: 256));

        var records = replay.Records
            .OrderBy(r => r.OrderingSequence)
            .ThenBy(r => r.RecordId.Value, StringComparer.Ordinal)
            .ToList();

        var ordering = records.Select(r => r.OrderingSequence).ToArray();
        var operationIds = records.Select(r => r.IdempotencyKey).ToArray();
        var payloads = records.Select(r => r.PayloadJson).ToArray();

        // Hash of concatenated ordered envelopes (sequence|id|payload) for byte-stable equality.
        var material = string.Join(
            "\n",
            records.Select(r =>
                $"{r.OrderingSequence}|{r.RecordId.Value}|{r.IdempotencyKey}|{r.PayloadJson}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));

        // Physical partition directory fingerprint when available under store root.
        var fileFingerprint = CapturePartitionFileFingerprint(harness);

        return new LegacyPartitionSnapshot(
            records.Count,
            ordering,
            operationIds,
            payloads,
            hash,
            fileFingerprint);
    }

    private static string CapturePartitionFileFingerprint(Phase22ContinuityTestSupport.Harness harness)
    {
        // Durable file store layout: agents-durable/<workspaceKey>/ under settings dir.
        // Tests use Phase21ContinuityTestSupport.CreateStore with a temp root; walk that root.
        var root = harness.StoreRootDirectory;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return "no-root";
        }

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var info = new FileInfo(path);
                var relative = Path.GetRelativePath(root, path);
                var bytes = File.ReadAllBytes(path);
                var fileHash = Convert.ToHexString(SHA256.HashData(bytes));
                return $"{relative}|{info.Length}|{fileHash}";
            });
        return string.Join("\n", files);
    }

    private sealed record LegacyPartitionSnapshot(
        int RecordCount,
        long[] OrderingSequences,
        string[] OperationIds,
        string[] Payloads,
        string SerializedBytesSha256,
        string FilePathsAndSizes);

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
