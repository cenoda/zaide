using System;
using System.IO;
using Xunit;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Tests.Features.Agents.Transparency.Storage;

/// <summary>
/// Phase 21 M1 workspace isolation tests.
/// </summary>
public sealed class Phase21WorkspaceIsolationTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _firstWorkspaceKey;
    private readonly AgentDurableWorkspaceStorageKey _secondWorkspaceKey;

    public Phase21WorkspaceIsolationTests()
    {
        _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Isolation_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);

        var firstRoot = Path.Combine(_rootDirectory, "workspace-a");
        var secondRoot = Path.Combine(_rootDirectory, "workspace-b");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);

        _firstWorkspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(firstRoot);
        _secondWorkspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(secondRoot);
    }

    public void Dispose() => Phase21StorageTestSupport.DeleteDirectory(_rootDirectory);

    [Fact]
    public void DifferentWorkspaceRoots_UseDistinctPartitions()
    {
        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);

        store.LoadWorkspace(_firstWorkspaceKey);
        store.LoadWorkspace(_secondWorkspaceKey);

        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _firstWorkspaceKey,
            AgentDurableRecordClass.Trace,
            "trace-a"));
        store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _secondWorkspaceKey,
            AgentDurableRecordClass.Trace,
            "trace-b"));

        var firstReplay = store.Replay(new AgentDurableRecordReplayRequest(
            _firstWorkspaceKey,
            AgentDurableRecordClass.Trace));
        var secondReplay = store.Replay(new AgentDurableRecordReplayRequest(
            _secondWorkspaceKey,
            AgentDurableRecordClass.Trace));

        Assert.Single(firstReplay.Records);
        Assert.Single(secondReplay.Records);
        Assert.Equal(_firstWorkspaceKey, firstReplay.Records[0].WorkspaceKey);
        Assert.Equal(_secondWorkspaceKey, secondReplay.Records[0].WorkspaceKey);
        Assert.NotEqual(firstReplay.Records[0].RecordId, secondReplay.Records[0].RecordId);
    }

    [Fact]
    public void ReloadedStore_DoesNotLeakRecordsAcrossWorkspaces()
    {
        using (var store = Phase21StorageTestSupport.CreateStore(_rootDirectory))
        {
            store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
                _firstWorkspaceKey,
                AgentDurableRecordClass.Audit,
                "audit-a"));
        }

        using var reload = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        var firstReplay = reload.Replay(new AgentDurableRecordReplayRequest(
            _firstWorkspaceKey,
            AgentDurableRecordClass.Audit));
        var secondReplay = reload.Replay(new AgentDurableRecordReplayRequest(
            _secondWorkspaceKey,
            AgentDurableRecordClass.Audit));

        Assert.Single(firstReplay.Records);
        Assert.Empty(secondReplay.Records);
    }
}
