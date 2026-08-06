using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Agents.Transparency.Integration;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

public sealed class Phase22UsageSurfaceTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22UsageSurfaceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "Phase22UsageSurface_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        Phase23IsolatedSettingsTestSupport.ConfigureIsolatedSettings(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot)));
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        _provider = services.BuildServiceProvider();
        _ = _provider.GetRequiredService<AgentTransparencySettingsSync>();
    }

    [Fact]
    public async Task UsageSurface_IsReachableAndUsesOpenedWorkspace()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        Assert.False(management.IsUsagePanelOpen);

        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.True(management.IsUsagePanelOpen);
        Assert.Equal(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);
        Assert.NotNull(management.UsageInspection.Summary);
        Assert.NotEqual(
            PathDerivedAgentDurableWorkspaceStorageKeyResolver.UnboundWorkspaceKey,
            management.UsageInspection.Summary!.WorkspaceKey.Value);

        management.CloseUsageCommand.Execute().Subscribe();
        Assert.False(management.IsUsagePanelOpen);
    }

    [Fact]
    public void UsageSurface_CommandRegistrationUsesAgentCategoryAndNoGesture()
    {
        var registry = _provider.GetRequiredService<ICommandRegistry>();
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        AgentTransparencyCommandRegistration.Register(registry, management);

        var descriptor = Assert.Single(registry.GetAll(), command => command.Id == "agent.usage.open");
        Assert.Equal("Open Agent Usage", descriptor.DisplayName);
        Assert.Equal("Agent", descriptor.Category);
        Assert.Empty(descriptor.DefaultGestures);
    }

    [Fact]
    public void Townhall_ReceivesTheProductionTransparencyManagementOwner()
    {
        var townhall = _provider.GetRequiredService<TownhallViewModel>();
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        Assert.Same(management, townhall.TransparencyManagement);
    }

    [Fact]
    public async Task UsageSurface_ExplicitCaptureToggleAndRecordProjection()
    {
        var settings = _provider.GetRequiredService<ISettingsService>();
        await Phase23SettingsTestSupport.DisableUsageCaptureAsync(settings);

        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var coordinator = _provider.GetRequiredService<AgentUsageCoordinator>();
        var inspection = management.UsageInspection;

        Assert.False(management.UsageAvailability.CurrentState.CaptureEnabled);

        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Empty, inspection.SurfaceState);

        await Phase23SettingsTestSupport.EnableUsageCaptureAsync(settings);
        await management.RefreshUsageSurfaceAsync();
        Assert.True(management.UsageAvailability.CurrentState.CaptureEnabled);

        var workspaceKey = coordinator.ResolveWorkspaceKey(_workspaceRoot);
        var result = coordinator.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.RequestCount,
            AgentUsageValueOrigin.Measured,
            "requests",
            "count",
            1,
            idempotencyKey: "surface-req-1",
            aggregationSemantics: AgentUsageAggregationSemantics.Delta));
        Assert.Equal(AgentUsageCaptureStatus.Accepted, result.Status);

        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Ready, inspection.SurfaceState);
        Assert.Single(inspection.Records);
        Assert.Equal(AgentUsageValueOrigin.Measured, inspection.Records[0].Origin);
        Assert.Equal(AgentUsageAggregationSemantics.Delta, inspection.Records[0].AggregationSemantics);

        management.SelectUsageRecord(inspection.Records[0].OrderingSequence);
        Assert.NotNull(inspection.SelectedRecord);
        Assert.Equal("requests", inspection.SelectedRecord!.MetricName);

        await Phase23SettingsTestSupport.DisableUsageCaptureAsync(settings);
        await management.RefreshUsageSurfaceAsync();
        Assert.False(management.UsageAvailability.CurrentState.CaptureEnabled);
    }

    [Fact]
    public async Task UsageSurface_EmptyIsDistinctFromFailedAndUnavailable()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var authority = (FakeWorkspaceActionAuthority)_provider.GetRequiredService<IWorkspaceActionAuthority>();
        var inspection = management.UsageInspection;

        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Empty, inspection.SurfaceState);
        Assert.Null(inspection.FailureReason);
        Assert.Contains("No usage or cost evidence", inspection.StatusCaption, StringComparison.Ordinal);

        authority.HasWorkspace = false;
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Unavailable, inspection.SurfaceState);
        Assert.NotEqual(AgentUsageSurfaceState.Empty, inspection.SurfaceState);
        Assert.Contains("workspace", inspection.StatusCaption, StringComparison.OrdinalIgnoreCase);
        Assert.Null(inspection.SelectedRecord);

        authority.HasWorkspace = true;
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Empty, inspection.SurfaceState);
    }

    [Fact]
    public void UsageSurface_FailedLoadNeverLooksLikeEmpty()
    {
        var (rootDirectory, workspaceKey) = Phase21UsageTestSupport.CreateWorkspaceFixture();
        try
        {
            var store = Phase21UsageTestSupport.CreateStore(rootDirectory);
            var coordinator = Phase21UsageTestSupport.CreateCoordinator(store);
            var availability = new AgentUsageAvailabilityProjection(
                coordinator,
                () => Path.Combine(rootDirectory, "workspace"));
            var authority = new FakeWorkspaceActionAuthority(
                FakeWorkspaceActionAuthority.CreateScopeFromDirectory(
                    Path.Combine(rootDirectory, "workspace")));
            var inspection = new AgentUsageInspectionViewModel(
                coordinator,
                availability,
                authority);

            store.Dispose();
            inspection.ReloadNow();

            Assert.Equal(AgentUsageSurfaceState.Failed, inspection.SurfaceState);
            Assert.NotEqual(AgentUsageSurfaceState.Empty, inspection.SurfaceState);
            Assert.False(
                string.Equals(
                    inspection.StatusCaption,
                    "No usage or cost evidence for the opened workspace.",
                    StringComparison.Ordinal));
            Assert.NotNull(inspection.FailureReason);
            Assert.True(inspection.CanRetry);
            Assert.Null(inspection.SelectedRecord);
            Assert.Empty(inspection.Records);
            _ = workspaceKey;
        }
        finally
        {
            Phase21UsageTestSupport.DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task UsageSurface_MissingCostIsNotPresentedAsVerifiedZero()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var coordinator = _provider.GetRequiredService<AgentUsageCoordinator>();
        var inspection = management.UsageInspection;

        management.OpenUsageCommand.Execute().Subscribe();
        await Phase23SettingsTestSupport.EnableUsageCaptureAsync(_provider.GetRequiredService<ISettingsService>());

        var workspaceKey = coordinator.ResolveWorkspaceKey(_workspaceRoot);
        Assert.Equal(
            AgentUsageCaptureStatus.Accepted,
            coordinator.TrySubmit(Phase21UsageTestSupport.CreateRequest(
                workspaceKey,
                Phase21UsageTestSupport.NativeHarnessBackendId,
                AgentUsageKind.TotalCost,
                AgentUsageValueOrigin.Unavailable,
                "cost",
                "currency",
                0,
                idempotencyKey: "surface-cost-unavailable",
                aggregationSemantics: AgentUsageAggregationSemantics.PointInTime)).Status);

        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Ready, inspection.SurfaceState);
        Assert.NotNull(inspection.Summary);
        Assert.False(inspection.Summary!.HasVerifiedTotalCost);
        Assert.Equal(0m, inspection.Summary.TotalCostValue);
        Assert.Null(inspection.Summary.TotalCostCurrency);
        Assert.Contains("unavailable", inspection.StatusCaption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invoice", inspection.StatusCaption, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
        catch
        {
        }
    }
}
