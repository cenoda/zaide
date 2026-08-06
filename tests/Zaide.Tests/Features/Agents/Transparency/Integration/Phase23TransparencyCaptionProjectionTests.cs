using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 23 F2: empty-state caption projection must not duplicate the primary
/// status line; policy help may appear at most once in the summary role.
/// </summary>
public sealed class Phase23TransparencyCaptionProjectionTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase23TransparencyCaptionProjectionTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase23TransparencyCaptions_" + Guid.NewGuid().ToString("N"));
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
    public void TracePanel_EmptyCaptureDisabled_ProjectsStatusOnceWithoutSummaryChrome()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenTraceCommand.Execute().Subscribe();
        management.RefreshTracePresentation();

        var trace = new AgentTracePanel();
        try
        {
            trace.SetViewModel(management);

            var statusText = trace.StatusCaptionControl.Text ?? string.Empty;
            Assert.Contains("Capture off", statusText, StringComparison.OrdinalIgnoreCase);
            Assert.False(trace.SummaryCaptionControl.IsVisible);
        }
        finally
        {
            trace.Dispose();
        }
    }

    [Fact]
    public async Task MemoryPanel_Empty_ProjectsPrimaryCaptionOnlyInStatus()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);

        var memory = new AgentMemoryPanel();
        try
        {
            memory.SetViewModel(management);

            Assert.Contains(
                "No durable memory records",
                memory.StatusCaptionControl.Text,
                StringComparison.Ordinal);
            Assert.True(string.IsNullOrEmpty(memory.SummaryCaptionControl.Text));
        }
        finally
        {
            memory.Dispose();
        }
    }

    [Fact]
    public async Task UsagePanel_Empty_ProjectsStatusOnceAndPolicyHelpInSummary()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);

        var usage = new AgentUsagePanel();
        try
        {
            usage.SetViewModel(management);

            var statusText = usage.StatusCaptionControl.Text ?? string.Empty;
            var summaryText = usage.SummaryCaptionControl.Text ?? string.Empty;
            Assert.Contains("No usage or cost evidence", statusText, StringComparison.Ordinal);
            Assert.DoesNotContain(statusText, summaryText, StringComparison.Ordinal);
            Assert.Contains("not zero", summaryText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            usage.Dispose();
        }
    }

    [Fact]
    public async Task MemoryPanel_Unavailable_DoesNotDualWriteStatusIntoSummary()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var authority = (FakeWorkspaceActionAuthority)_provider.GetRequiredService<IWorkspaceActionAuthority>();
        management.OpenMemoryCommand.Execute().Subscribe();
        authority.HasWorkspace = false;
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Unavailable, management.MemoryInspection.SurfaceState);

        var memory = new AgentMemoryPanel();
        try
        {
            memory.SetViewModel(management);

            var statusText = memory.StatusCaptionControl.Text ?? string.Empty;
            var summaryText = memory.SummaryCaptionControl.Text ?? string.Empty;
            Assert.Contains("workspace", statusText, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(statusText));
            Assert.True(string.IsNullOrEmpty(summaryText));
            Assert.DoesNotContain(statusText, summaryText, StringComparison.Ordinal);
        }
        finally
        {
            memory.Dispose();
        }
    }

    [Fact]
    public async Task UsagePanel_Unavailable_DoesNotDualWriteStatusIntoSummary()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var authority = (FakeWorkspaceActionAuthority)_provider.GetRequiredService<IWorkspaceActionAuthority>();
        management.OpenUsageCommand.Execute().Subscribe();
        authority.HasWorkspace = false;
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Unavailable, management.UsageInspection.SurfaceState);

        var usage = new AgentUsagePanel();
        try
        {
            usage.SetViewModel(management);

            var statusText = usage.StatusCaptionControl.Text ?? string.Empty;
            var summaryText = usage.SummaryCaptionControl.Text ?? string.Empty;
            Assert.Contains("workspace", statusText, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(statusText));
            Assert.True(string.IsNullOrEmpty(summaryText));
            Assert.DoesNotContain(statusText, summaryText, StringComparison.Ordinal);
        }
        finally
        {
            usage.Dispose();
        }
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
