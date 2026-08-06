using System;
using System.IO;
using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Agents.Transparency.Integration;

namespace Zaide.Tests.Features.Agents.Transparency.Trace;

public sealed class Phase22TraceSurfaceTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22TraceSurfaceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "Phase22TraceSurface_" + Guid.NewGuid().ToString("N"));
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
    public async System.Threading.Tasks.Task TraceSurface_UsesOpenedWorkspaceAndSettingsCaptureDefault()
    {
        var settings = _provider.GetRequiredService<ISettingsService>();
        await Phase23SettingsTestSupport.DisableTraceCaptureAsync(settings);

        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        Assert.False(management.IsTracePanelOpen);
        Assert.False(management.TraceAvailability.CurrentState.CaptureEnabled);

        management.OpenTraceCommand.Execute().Subscribe();
        Assert.True(management.IsTracePanelOpen);

        await Phase23SettingsTestSupport.EnableTraceCaptureAsync(settings);
        management.RefreshTracePresentation();
        Assert.True(management.TraceAvailability.CurrentState.CaptureEnabled);

        var summary = await management.LoadTraceSummaryAsync();
        Assert.NotEqual("ws:unbound", summary.WorkspaceKey.Value);

        await Phase23SettingsTestSupport.DisableTraceCaptureAsync(settings);
        management.RefreshTracePresentation();
        Assert.False(management.TraceAvailability.CurrentState.CaptureEnabled);
    }

    [Fact]
    public void TraceSurface_CommandRegistrationUsesAgentCategoryAndNoGesture()
    {
        var registry = _provider.GetRequiredService<ICommandRegistry>();
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        AgentTransparencyCommandRegistration.Register(registry, management);

        var descriptor = Assert.Single(registry.GetAll(), command => command.Id == "agent.trace.open");
        Assert.Equal("Open Agent Trace", descriptor.DisplayName);
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

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_workspaceRoot, recursive: true);
    }
}
