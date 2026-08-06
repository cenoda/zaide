using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Agents.Memory.Store;
using Zaide.Tests.Features.Agents.Transparency.Usage;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 22.4 M4: integrated failure / empty / unavailable states never
/// masquerade as each other across the transparency management surface.
/// </summary>
public sealed class Phase22TransparencyFailureStateTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22TransparencyFailureStateTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase22TransparencyFailure_" + Guid.NewGuid().ToString("N"));
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
    public async Task IntegratedEmpty_IsDistinctFromUnavailable_AcrossMemoryAndUsage()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var authority = (FakeWorkspaceActionAuthority)_provider.GetRequiredService<IWorkspaceActionAuthority>();

        management.OpenTraceCommand.Execute().Subscribe();
        management.OpenMemoryCommand.Execute().Subscribe();
        management.OpenUsageCommand.Execute().Subscribe();

        management.RefreshTracePresentation();
        await management.RefreshMemorySurfaceAsync();
        await management.RefreshUsageSurfaceAsync();

        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);
        Assert.Equal(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);
        Assert.Contains("Capture off", management.TraceStatusCaption, StringComparison.OrdinalIgnoreCase);

        authority.HasWorkspace = false;
        management.RefreshTracePresentation();
        await management.RefreshMemorySurfaceAsync();
        await management.RefreshUsageSurfaceAsync();

        Assert.Equal(AgentMemorySurfaceState.Unavailable, management.MemoryInspection.SurfaceState);
        Assert.NotEqual(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);
        Assert.Equal(AgentUsageSurfaceState.Unavailable, management.UsageInspection.SurfaceState);
        Assert.NotEqual(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);
        Assert.Null(management.MemoryInspection.SelectedRecord);
        Assert.Null(management.UsageInspection.SelectedRecord);

        authority.HasWorkspace = true;
        await management.RefreshMemorySurfaceAsync();
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);
        Assert.Equal(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);
    }

    [Fact]
    public void FailedMemoryAndUsageLoads_NeverLookLikeEmpty_AndSupportBoundedRetry()
    {
        var (memoryRoot, _, memoryWorkspace) = Phase21MemoryTestSupport.CreateWorkspaceFixture();
        var (usageRoot, _) = Phase21UsageTestSupport.CreateWorkspaceFixture();
        var usageWorkspace = Path.Combine(usageRoot, "workspace");
        try
        {
            var memoryStore = Phase21MemoryTestSupport.CreateStore(memoryRoot);
            var memoryCoordinator = Phase21MemoryTestSupport.CreateCoordinator(memoryStore);
            var memoryAvailability = new AgentMemoryAvailabilityProjection(
                memoryCoordinator,
                () => memoryWorkspace);
            var catalog = _provider.GetRequiredService<IActorCatalog>();
            var memoryAuthority = new FakeWorkspaceActionAuthority(
                FakeWorkspaceActionAuthority.CreateScopeFromDirectory(memoryWorkspace));
            var memoryInspection = new AgentMemoryInspectionViewModel(
                memoryCoordinator,
                memoryAvailability,
                catalog,
                memoryAuthority);

            memoryStore.Dispose();
            memoryInspection.ReloadNow();
            Assert.Equal(AgentMemorySurfaceState.Failed, memoryInspection.SurfaceState);
            Assert.NotEqual(AgentMemorySurfaceState.Empty, memoryInspection.SurfaceState);
            Assert.NotNull(memoryInspection.FailureReason);
            Assert.True(memoryInspection.CanRetry);
            Assert.True(memoryInspection.RetryAttempts < AgentMemoryInspectionViewModel.MaxRetryAttempts);

            var usageStore = Phase21UsageTestSupport.CreateStore(usageRoot);
            var usageSink = Phase21UsageTestSupport.CreateSink(usageStore);
            var usageCoordinator = Phase21UsageTestSupport.CreateCoordinator(usageStore, usageSink);
            var usageAvailability = new AgentUsageAvailabilityProjection(
                usageCoordinator,
                () => usageWorkspace);
            var usageAuthority = new FakeWorkspaceActionAuthority(
                FakeWorkspaceActionAuthority.CreateScopeFromDirectory(usageWorkspace));
            var usageInspection = new AgentUsageInspectionViewModel(
                usageCoordinator,
                usageAvailability,
                usageAuthority);

            usageStore.Dispose();
            usageInspection.ReloadNow();
            Assert.Equal(AgentUsageSurfaceState.Failed, usageInspection.SurfaceState);
            Assert.NotEqual(AgentUsageSurfaceState.Empty, usageInspection.SurfaceState);
            Assert.NotNull(usageInspection.FailureReason);
            Assert.True(usageInspection.CanRetry);
            Assert.True(usageInspection.RetryAttempts < AgentUsageInspectionViewModel.MaxRetryAttempts);
        }
        finally
        {
            Phase21MemoryTestSupport.DeleteDirectory(memoryRoot);
            Phase21UsageTestSupport.DeleteDirectory(usageRoot);
        }
    }

    [Fact]
    public async Task ReadyMemory_DoesNotCollapseToEmpty_WhenRecordsExist()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        await management.BindMemoryTownhallContextAsync(
            new AgentMemoryInspectionViewModel.TownhallContext(
                conversationId: ConversationId.ForChannel("general"),
                agentActorId: ActorId.TownhallAgent,
                sessionId: null,
                projectId: null));
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();

        management.MemoryInspection.SelectedScope = AgentMemoryScope.ProjectShared;
        management.MemoryInspection.DraftContent = "failure-state ready record";
        Assert.Equal(AgentMemoryOperationStatus.Accepted, management.CreateMemoryFromDraft().Status);
        Assert.Equal(AgentMemorySurfaceState.Ready, management.MemoryInspection.SurfaceState);
        Assert.NotEqual(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);
        Assert.NotNull(management.MemoryInspection.SelectedRecord);
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
