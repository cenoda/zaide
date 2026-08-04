using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 22.4 M4: lifecycle Backup safety for missing/unavailable partitions
/// before any user-reachable Backup path depends on a clean package return.
/// Restore and Migrate remain application-only (no UI covered here).
/// </summary>
public sealed class Phase22TransparencyBackupTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22TransparencyBackupTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase22TransparencyBackup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot)));
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void Backup_MissingPartitionDirectory_ReturnsNotFoundWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), "ZaideM4BackupMissing_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = Phase21TransparencyIntegrationTestSupport.CreateStore(root);
            var memoryCoordinator = Phase21TransparencyIntegrationTestSupport.CreateMemoryCoordinator(store);
            var memoryLifecycle = new AgentMemoryLifecycleService(memoryCoordinator.Inspector);
            var coordinator = new AgentTransparencyLifecycleCoordinator(store, memoryLifecycle);

            var missingKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(
                Path.Combine(root, "never-created-workspace"));
            var package = coordinator.Backup(missingKey);

            Assert.Equal(AgentTransparencyLifecycleStatus.NotFound, package.Status);
            Assert.True(string.IsNullOrEmpty(package.BackupDirectory));
            Assert.Contains("not found", package.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            store.Dispose();
        }
        finally
        {
            Phase21TransparencyIntegrationTestSupport.DeleteDirectory(root);
        }
    }

    [Fact]
    public void BackupPackage_AcceptedRequiresDirectory_FailureAllowsEmptyPath()
    {
        var key = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(
            Path.Combine(Path.GetTempPath(), "ws-" + Guid.NewGuid().ToString("N")));

        var failure = new AgentTransparencyBackupPackage(
            key,
            backupDirectory: string.Empty,
            DateTimeOffset.UtcNow,
            AgentTransparencyLifecycleStatus.NotFound,
            "Workspace partition directory not found.");
        Assert.Equal(AgentTransparencyLifecycleStatus.NotFound, failure.Status);
        Assert.Equal(string.Empty, failure.BackupDirectory);

        var rejected = new AgentTransparencyBackupPackage(
            key,
            backupDirectory: string.Empty,
            DateTimeOffset.UtcNow,
            AgentTransparencyLifecycleStatus.Rejected,
            "Workspace partition unavailable: Quarantined");
        Assert.Equal(AgentTransparencyLifecycleStatus.Rejected, rejected.Status);

        Assert.Throws<ArgumentException>(() =>
            new AgentTransparencyBackupPackage(
                key,
                backupDirectory: string.Empty,
                DateTimeOffset.UtcNow,
                AgentTransparencyLifecycleStatus.Accepted));
    }

    [Fact]
    public async Task ManagementBackupAsync_CleanOpenedWorkspace_Succeeds()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        management.MemoryInspection.SelectedScope = AgentMemoryScope.ProjectShared;
        management.MemoryInspection.DraftContent = "backup-ready record";
        Assert.Equal(
            AgentMemoryOperationStatus.Accepted,
            management.CreateMemoryFromDraft().Status);

        var package = await management.BackupAsync(_workspaceRoot);
        Assert.Equal(AgentTransparencyLifecycleStatus.Accepted, package.Status);
        Assert.True(Directory.Exists(package.BackupDirectory));
        Assert.True(Directory.EnumerateFileSystemEntries(package.BackupDirectory).Any());

        try
        {
            Directory.Delete(package.BackupDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Backup_ThenRestore_RoundTripStillPreservesPartition()
    {
        var (rootDirectory, workspaceKey) = Phase21TransparencyIntegrationTestSupport.CreateWorkspaceFixture();
        var store = Phase21TransparencyIntegrationTestSupport.CreateStore(rootDirectory);
        try
        {
            var memoryCoordinator = Phase21TransparencyIntegrationTestSupport.CreateMemoryCoordinator(store);
            var memoryLifecycle = new AgentMemoryLifecycleService(memoryCoordinator.Inspector);
            var coordinator = new AgentTransparencyLifecycleCoordinator(store, memoryLifecycle);

            memoryCoordinator.Create(new AgentMemoryCreateRequest(
                workspaceKey,
                Phase21TransparencyIntegrationTestSupport.CreateAgentScope(),
                "Backup round-trip",
                Phase21TransparencyIntegrationTestSupport.CreateProvenance(),
                idempotencyKey: "m4-backup-roundtrip"));

            var backup = coordinator.Backup(workspaceKey);
            Assert.Equal(AgentTransparencyLifecycleStatus.Accepted, backup.Status);
            Assert.True(Directory.Exists(backup.BackupDirectory));

            var restore = coordinator.Restore(workspaceKey, backup.BackupDirectory);
            Assert.Equal(AgentTransparencyLifecycleStatus.Accepted, restore.Status);

            var export = coordinator.Export(workspaceKey);
            var memorySection = export.Sections.Single(s => s.RecordClass == AgentDurableRecordClass.Memory);
            Assert.Equal(1, memorySection.RecordCount);

            try
            {
                Directory.Delete(backup.BackupDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
        finally
        {
            store.Dispose();
            Phase21TransparencyIntegrationTestSupport.DeleteDirectory(rootDirectory);
        }
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
