using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Memory.Store;

public sealed class Phase21MemoryLifecycleTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentMemoryCoordinator _coordinator;
    private readonly AgentMemoryLifecycleService _lifecycle;

    public Phase21MemoryLifecycleTests()
    {
        (_rootDirectory, _workspaceKey, _) = Phase21MemoryTestSupport.CreateWorkspaceFixture();
        _store = Phase21MemoryTestSupport.CreateStore(_rootDirectory);
        _coordinator = Phase21MemoryTestSupport.CreateCoordinator(_store);
        _lifecycle = new AgentMemoryLifecycleService(_coordinator.Inspector);
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21MemoryTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Export_IncludesSchemaVersionAndProvenance()
    {
        _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Exportable fact",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "export-rev"),
            idempotencyKey: "export-1"));

        var package = _lifecycle.Export(_workspaceKey);

        Assert.Equal(AgentMemoryLimits.PayloadSchemaVersion, package.SchemaVersion);
        Assert.False(package.PartialUnavailable);
        var record = Assert.Single(package.Records);
        Assert.Equal("export-rev", record.Provenance.SourceRevision);
    }

    [Fact]
    public void Backup_PreservesDeletedTombstonesForAudit()
    {
        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Backup me",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "backup-create"));

        _coordinator.Delete(new AgentMemoryDeleteRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "backup-delete"));

        var backup = _lifecycle.Backup(_workspaceKey);
        var tombstone = Assert.Single(backup.Records);
        Assert.Equal(AgentMemoryStatus.Deleted, tombstone.Status);
    }

    [Fact]
    public void Replay_IdempotentOperationsPreserveSingleLogicalOutcome()
    {
        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Replay test",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "replay-create"));

        _coordinator.Disable(new AgentMemoryDisableRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "replay-disable"));

        _coordinator.Disable(new AgentMemoryDisableRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "replay-disable"));

        var record = _coordinator.Inspector.TryGetRecord(_workspaceKey, created.MemoryId!.Value);
        Assert.Equal(AgentMemoryStatus.Disabled, record!.Status);

        var envelopes = Phase21MemoryTestSupport.ReplayMemoryRecords(_store, _workspaceKey);
        Assert.Equal(2, envelopes.Count);
    }

    [Fact]
    public void Inspector_SummaryReflectsLifecycleCounts()
    {
        var first = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "One",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "summary-1"));

        _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateConversationScope(),
            "Two",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "summary-2"));

        _coordinator.Disable(new AgentMemoryDisableRequest(
            _workspaceKey,
            first.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "summary-disable"));

        var summary = _coordinator.Inspector.GetSummary(_workspaceKey);
        Assert.Equal(2, summary.TotalRecords);
        Assert.Equal(1, summary.ActiveRecords);
        Assert.Equal(1, summary.DisabledRecords);
    }

    [Fact]
    public void Migration_UsesM1MemoryRecordClassPartition()
    {
        _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateProjectScope(),
            "Partition check",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "migration-1"));

        var recordsDir = System.IO.Path.Combine(
            _rootDirectory,
            _workspaceKey.Value,
            "records",
            "Memory");

        Assert.True(System.IO.Directory.Exists(recordsDir));
        Assert.NotEmpty(System.IO.Directory.GetFiles(recordsDir, "*.json"));
    }

    [Fact]
    public void Retention_DefaultIsUserControlled_NoAutomaticExpiry()
    {
        Assert.Equal(0, AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Memory));
    }
}
