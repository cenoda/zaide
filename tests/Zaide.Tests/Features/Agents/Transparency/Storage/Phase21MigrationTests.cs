using System;
using System.IO;
using Xunit;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Storage;

/// <summary>
/// Phase 21 M1 migration, rollback, and unknown-version tests.
/// </summary>
public sealed class Phase21MigrationTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;

    public Phase21MigrationTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21StorageTestSupport.CreateWorkspaceFixture();
    }

    public void Dispose() => Phase21StorageTestSupport.DeleteDirectory(_rootDirectory);

    [Fact]
    public void Load_MigratesV0IndexWithPreMigrationBackup()
    {
        var workspaceDirectory = Path.Combine(_rootDirectory, _workspaceKey.Value);
        Directory.CreateDirectory(workspaceDirectory);
        var indexPath = AgentDurableRecordPathResolver.GetIndexPath(workspaceDirectory);
        File.WriteAllText(
            indexPath,
            """
            {
              "schemaVersion": 0,
              "workspaceKey": "__PLACEHOLDER__",
              "sequences": {
                "Trace": 3
              },
              "records": []
            }
            """.Replace("__PLACEHOLDER__", _workspaceKey.Value, System.StringComparison.Ordinal));

        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        var outcome = store.LoadWorkspace(_workspaceKey);

        Assert.Equal(AgentDurableRecordLoadOutcome.Migrated, outcome);
        Assert.True(File.Exists(AgentDurableRecordPathResolver.GetPreMigrationBackupPath(workspaceDirectory)));

        var migratedJson = File.ReadAllText(indexPath);
        Assert.Contains("\"schemaVersion\":1", migratedJson, System.StringComparison.Ordinal);
        Assert.Contains("\"classState\"", migratedJson, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnsupportedFutureVersion_DisablesWrites()
    {
        var workspaceDirectory = Path.Combine(_rootDirectory, _workspaceKey.Value);
        Directory.CreateDirectory(workspaceDirectory);
        var indexPath = AgentDurableRecordPathResolver.GetIndexPath(workspaceDirectory);
        File.WriteAllText(
            indexPath,
            $$"""
              {
                "schemaVersion": 99,
                "workspaceKey": "{{_workspaceKey.Value}}",
                "classState": {},
                "records": []
              }
              """);

        using var store = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        Assert.Equal(AgentDurableRecordLoadOutcome.UnsupportedVersion, store.LoadWorkspace(_workspaceKey));

        var append = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
            _workspaceKey,
            AgentDurableRecordClass.Trace,
            "trace-future"));
        Assert.Equal(AgentDurableRecordAppendStatus.WritesDisabled, append.Status);
    }

    [Fact]
    public void Load_QuarantinesUnreadableRecordWithoutDeletingCommittedIndex()
    {
        AgentDurableRecordAppendResult appendResult;
        using (var store = Phase21StorageTestSupport.CreateStore(_rootDirectory))
        {
            store.LoadWorkspace(_workspaceKey);
            appendResult = store.TryAppend(Phase21StorageTestSupport.CreateAppendRequest(
                _workspaceKey,
                AgentDurableRecordClass.Usage,
                "usage-good"));
            store.Flush();
        }

        var workspaceDirectory = Path.Combine(_rootDirectory, _workspaceKey.Value);
        var recordPath = AgentDurableRecordPathResolver.GetRecordPath(
            workspaceDirectory,
            AgentDurableRecordClass.Usage,
            orderingSequence: appendResult.Envelope!.OrderingSequence,
            recordIdValue: appendResult.Envelope.RecordId.Value);
        File.WriteAllText(recordPath, "{ not valid");

        using var reload = Phase21StorageTestSupport.CreateStore(_rootDirectory);
        var outcome = reload.LoadWorkspace(_workspaceKey);
        Assert.Equal(AgentDurableRecordLoadOutcome.Quarantined, outcome);

        var quarantineDir = AgentDurableRecordPathResolver.GetQuarantineDirectory(workspaceDirectory);
        Assert.True(Directory.Exists(quarantineDir));
        Assert.NotEmpty(Directory.GetFiles(quarantineDir));
    }
}
