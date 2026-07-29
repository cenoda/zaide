using System;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

public sealed class Phase21BackupTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentTransparencyLifecycleCoordinator _coordinator;

    public Phase21BackupTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21TransparencyIntegrationTestSupport.CreateWorkspaceFixture();
        _store = Phase21TransparencyIntegrationTestSupport.CreateStore(_rootDirectory);
        var memoryCoordinator = Phase21TransparencyIntegrationTestSupport.CreateMemoryCoordinator(_store);
        var memoryLifecycle = new AgentMemoryLifecycleService(memoryCoordinator.Inspector);
        _coordinator = new AgentTransparencyLifecycleCoordinator(_store, memoryLifecycle);

        memoryCoordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21TransparencyIntegrationTestSupport.CreateAgentScope(),
            "Backup me",
            Phase21TransparencyIntegrationTestSupport.CreateProvenance(),
            idempotencyKey: "backup-record"));
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21TransparencyIntegrationTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Backup_Restore_RoundTripPreservesPartition()
    {
        var backup = _coordinator.Backup(_workspaceKey);
        Assert.Equal(AgentTransparencyLifecycleStatus.Accepted, backup.Status);
        Assert.True(Directory.Exists(backup.BackupDirectory));

        var restore = _coordinator.Restore(_workspaceKey, backup.BackupDirectory);
        Assert.Equal(AgentTransparencyLifecycleStatus.Accepted, restore.Status);
        Assert.NotEqual(AgentDurableRecordLoadOutcome.Quarantined, restore.LoadOutcome);

        var export = _coordinator.Export(_workspaceKey);
        var memorySection = export.Sections.Single(s => s.RecordClass == AgentDurableRecordClass.Memory);
        Assert.Equal(1, memorySection.RecordCount);
    }
}
