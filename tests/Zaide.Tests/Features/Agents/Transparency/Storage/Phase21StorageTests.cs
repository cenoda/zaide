using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Storage;

/// <summary>
/// Phase 21 M1 durable storage behavior tests.
/// </summary>
public sealed class Phase21StorageTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;

    public Phase21StorageTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21StorageTestSupport.CreateWorkspaceFixture();
    }

    public void Dispose() => Phase21StorageTestSupport.DeleteDirectory(_rootDirectory);

    [Fact]
    public void Append_AssignsMonotonicOrderingPerRecordClass()
    {
        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        Assert.Equal(AgentDurableRecordLoadOutcome.Missing, store.LoadWorkspace(_workspaceKey));

        var first = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Trace,
            "trace-1"));
        var second = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Trace,
            "trace-2"));

        Assert.Equal(AgentDurableRecordAppendStatus.Appended, first.Status);
        Assert.Equal(AgentDurableRecordAppendStatus.Appended, second.Status);
        Assert.Equal(1, first.Envelope!.OrderingSequence);
        Assert.Equal(2, second.Envelope!.OrderingSequence);
    }

    [Fact]
    public void Append_IgnoresDuplicateIdempotencyKey()
    {
        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        store.LoadWorkspace(_workspaceKey);

        var first = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Audit,
            "audit-dup"));
        var duplicate = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Audit,
            "audit-dup",
            payloadJson: """{"marker":"changed"}"""));

        Assert.Equal(AgentDurableRecordAppendStatus.Appended, first.Status);
        Assert.Equal(AgentDurableRecordAppendStatus.DuplicateIgnored, duplicate.Status);
        Assert.Equal(first.Envelope!.RecordId, duplicate.Envelope?.RecordId);
    }

    [Fact]
    public void Replay_ReturnsOrderedRecordsAfterCursor()
    {
        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        store.LoadWorkspace(_workspaceKey);

        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Usage,
            "usage-1"));
        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Usage,
            "usage-2"));
        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Usage,
            "usage-3"));

        var firstPage = store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Usage,
            afterOrderingSequence: 0,
            maxRecords: 2));
        var secondPage = store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Usage,
            afterOrderingSequence: firstPage.NextCursor.AfterOrderingSequence));

        Assert.Equal(2, firstPage.Records.Count);
        Assert.Equal(new[] { 1L, 2L }, firstPage.Records.Select(r => r.OrderingSequence));
        Assert.Single(secondPage.Records);
        Assert.Equal(3L, secondPage.Records[0].OrderingSequence);
    }

    [Fact]
    public void InterruptedIndexWrite_DoesNotReplaceCommittedIndex()
    {
        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        store.LoadWorkspace(_workspaceKey);
        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Memory,
            "memory-1"));
        store.Flush();

        var workspaceDirectory = Path.Combine(_rootDirectory, _workspaceKey.Value);
        var indexPath = AgentDurableRecordPathResolver.GetIndexPath(workspaceDirectory);
        var tempPath = AgentDurableRecordPathResolver.GetIndexTempPath(workspaceDirectory);
        File.WriteAllText(tempPath, "{ not valid json");

        using var reload = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        Assert.Equal(AgentDurableRecordLoadOutcome.Loaded, reload.LoadWorkspace(_workspaceKey));
        var replay = reload.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Memory));
        Assert.Single(replay.Records);
    }

    [Fact]
    public async Task ConcurrentWriter_FailsClosedWithContention()
    {
        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        store.LoadWorkspace(_workspaceKey);
        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.SessionRecovery,
            "recovery-prime"));

        var workspaceDirectory = Path.Combine(_rootDirectory, _workspaceKey.Value);
        var lockPath = AgentDurableRecordPathResolver.GetLockPath(workspaceDirectory);
        using var externalLock = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        var result = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.SessionRecovery,
            "recovery-contention"));

        Assert.Equal(AgentDurableRecordAppendStatus.ContentionFailed, result.Status);
        await Task.CompletedTask;
    }
}
