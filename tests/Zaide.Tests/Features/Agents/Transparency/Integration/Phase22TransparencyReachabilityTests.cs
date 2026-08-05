using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 22.4 M4: integrated production reachability for Trace, Memory, and
/// Usage together — commands, Townhall buttons, and presentation states.
/// </summary>
public sealed class Phase22TransparencyReachabilityTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22TransparencyReachabilityTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase22TransparencyReachability_" + Guid.NewGuid().ToString("N"));
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
    public async Task IntegratedSurfaces_AreProductionReachableTogether()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var townhall = _provider.GetRequiredService<TownhallViewModel>();
        var registry = _provider.GetRequiredService<ICommandRegistry>();

        Assert.Same(management, townhall.TransparencyManagement);

        AgentTransparencyCommandRegistration.Register(registry, management);
        var commandIds = registry.GetAll().Select(c => c.Id).ToArray();
        Assert.Contains("agent.trace.open", commandIds);
        Assert.Contains("agent.memory.open", commandIds);
        Assert.Contains("agent.usage.open", commandIds);

        Assert.All(
            new[] { "agent.trace.open", "agent.memory.open", "agent.usage.open" },
            id =>
            {
                var descriptor = Assert.Single(registry.GetAll(), c => c.Id == id);
                Assert.Equal("Agent", descriptor.Category);
                Assert.Empty(descriptor.DefaultGestures);
            });

        Assert.True(registry.Execute("agent.trace.open"));
        Assert.True(management.IsTracePanelOpen);
        management.RefreshTracePresentation();
        Assert.False(management.TraceAvailability.CurrentState.CaptureEnabled);
        Assert.Contains("disabled", management.TraceStatusCaption, StringComparison.OrdinalIgnoreCase);

        Assert.True(registry.Execute("agent.memory.open"));
        Assert.True(management.IsMemoryPanelOpen);
        Assert.False(management.IsTracePanelOpen);
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);
        Assert.NotEqual(AgentMemorySurfaceState.Failed, management.MemoryInspection.SurfaceState);
        Assert.NotEqual(AgentMemorySurfaceState.Unavailable, management.MemoryInspection.SurfaceState);

        Assert.True(registry.Execute("agent.usage.open"));
        Assert.True(management.IsUsagePanelOpen);
        Assert.False(management.IsTracePanelOpen);
        Assert.False(management.IsMemoryPanelOpen);
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);
        Assert.NotEqual(AgentUsageSurfaceState.Failed, management.UsageInspection.SurfaceState);
        Assert.NotEqual(AgentUsageSurfaceState.Unavailable, management.UsageInspection.SurfaceState);

        // Mutual exclusivity: last open surface remains; siblings closed.
        Assert.False(management.IsTracePanelOpen);
        Assert.False(management.IsMemoryPanelOpen);
        Assert.True(management.IsUsagePanelOpen);
    }

    [Fact]
    public void TownhallEntryButtons_ExposeStableAutomationNamesAndTabStops()
    {
        // Construct the same named entry controls Townhall hosts without requiring
        // a full Avalonia Application (TownhallView depends on Application.Current).
        var trace = CreateEntryButton("Trace", "Open or close agent trace evidence");
        var memory = CreateEntryButton("Memory", "Open or close agent durable memory");
        var usage = CreateEntryButton("Usage", "Open or close agent usage and cost evidence");

        Assert.Equal("Open or close agent trace evidence", AutomationProperties.GetName(trace));
        Assert.Equal("Open or close agent durable memory", AutomationProperties.GetName(memory));
        Assert.Equal("Open or close agent usage and cost evidence", AutomationProperties.GetName(usage));
        Assert.True(trace.Focusable && trace.IsTabStop);
        Assert.True(memory.Focusable && memory.IsTabStop);
        Assert.True(usage.Focusable && usage.IsTabStop);

        // Source-level guarantee: TownhallView wires these exact names.
        var townhallSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "Features",
                "Townhall",
                "Presentation",
                "TownhallView.cs"));
        Assert.Contains("Open or close agent trace evidence", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Open or close agent durable memory", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Open or close agent usage and cost evidence", townhallSource, StringComparison.Ordinal);
        Assert.Contains("IsTabStop = true", townhallSource, StringComparison.Ordinal);
    }

    private static Avalonia.Controls.Button CreateEntryButton(string content, string automationName)
    {
        var button = new Avalonia.Controls.Button
        {
            Content = content,
            Focusable = true,
            IsTabStop = true,
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public async Task OpenedWorkspace_NeverResolvesUnboundOnUserPath()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        management.OpenTraceCommand.Execute().Subscribe();
        management.RefreshTracePresentation();
        var traceSummary = await management.LoadTraceSummaryAsync();
        Assert.NotEqual("ws:unbound", traceSummary.WorkspaceKey.Value);

        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.NotNull(management.MemoryInspection.Summary);
        Assert.NotEqual("ws:unbound", management.MemoryInspection.Summary!.WorkspaceKey.Value);

        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.NotNull(management.UsageInspection.Summary);
        Assert.NotEqual("ws:unbound", management.UsageInspection.Summary!.WorkspaceKey.Value);
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
