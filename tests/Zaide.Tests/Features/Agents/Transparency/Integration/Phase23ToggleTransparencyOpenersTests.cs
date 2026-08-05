using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 23: toolbar Trace/Memory/Usage openers toggle open/close; per-panel
/// Close works; opening one inspect surface closes the others (mutual exclusivity).
/// </summary>
public sealed class Phase23ToggleTransparencyOpenersTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase23ToggleTransparencyOpenersTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase23ToggleTransparency_" + Guid.NewGuid().ToString("N"));
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
    public void ToggleTrace_OpensThenCloses()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        Assert.False(management.IsTracePanelOpen);

        management.ToggleTraceCommand.Execute().Subscribe();
        Assert.True(management.IsTracePanelOpen);

        management.ToggleTraceCommand.Execute().Subscribe();
        Assert.False(management.IsTracePanelOpen);
    }

    [Fact]
    public async Task ToggleMemory_OpensThenCloses()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        Assert.False(management.IsMemoryPanelOpen);

        management.ToggleMemoryCommand.Execute().Subscribe();
        Assert.True(management.IsMemoryPanelOpen);
        await management.RefreshMemorySurfaceAsync();

        management.ToggleMemoryCommand.Execute().Subscribe();
        Assert.False(management.IsMemoryPanelOpen);
    }

    [Fact]
    public async Task ToggleUsage_OpensThenCloses()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        Assert.False(management.IsUsagePanelOpen);

        management.ToggleUsageCommand.Execute().Subscribe();
        Assert.True(management.IsUsagePanelOpen);
        await management.RefreshUsageSurfaceAsync();

        management.ToggleUsageCommand.Execute().Subscribe();
        Assert.False(management.IsUsagePanelOpen);
    }

    [Fact]
    public async Task CloseCommands_StillCloseWhenPanelIsOpen()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        management.ToggleTraceCommand.Execute().Subscribe();
        Assert.True(management.IsTracePanelOpen);
        management.CloseTraceCommand.Execute().Subscribe();
        Assert.False(management.IsTracePanelOpen);

        management.ToggleMemoryCommand.Execute().Subscribe();
        Assert.True(management.IsMemoryPanelOpen);
        await management.RefreshMemorySurfaceAsync();
        management.CloseMemoryCommand.Execute().Subscribe();
        Assert.False(management.IsMemoryPanelOpen);

        management.ToggleUsageCommand.Execute().Subscribe();
        Assert.True(management.IsUsagePanelOpen);
        await management.RefreshUsageSurfaceAsync();
        management.CloseUsageCommand.Execute().Subscribe();
        Assert.False(management.IsUsagePanelOpen);
    }

    [Fact]
    public async Task ToggleOpeningOnePanel_ClosesTheOthers()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        management.ToggleTraceCommand.Execute().Subscribe();
        Assert.True(management.IsTracePanelOpen);
        Assert.False(management.IsMemoryPanelOpen);
        Assert.False(management.IsUsagePanelOpen);

        management.ToggleMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.False(management.IsTracePanelOpen);
        Assert.True(management.IsMemoryPanelOpen);
        Assert.False(management.IsUsagePanelOpen);

        management.ToggleUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.False(management.IsTracePanelOpen);
        Assert.False(management.IsMemoryPanelOpen);
        Assert.True(management.IsUsagePanelOpen);
    }

    [Fact]
    public void OpenCommand_AlsoEnforcesMutualExclusivity()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        management.OpenTraceCommand.Execute().Subscribe();
        Assert.True(management.IsTracePanelOpen);

        management.OpenMemoryCommand.Execute().Subscribe();
        Assert.False(management.IsTracePanelOpen);
        Assert.True(management.IsMemoryPanelOpen);
        Assert.False(management.IsUsagePanelOpen);

        management.OpenUsageCommand.Execute().Subscribe();
        Assert.False(management.IsTracePanelOpen);
        Assert.False(management.IsMemoryPanelOpen);
        Assert.True(management.IsUsagePanelOpen);
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
