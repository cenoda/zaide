using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents.Transparency.Integration;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 23 F3: empty inspect surfaces render minimal chrome; failed/unavailable
/// keep full inspection controls.
/// </summary>
public sealed class Phase23EmptyInspectChromeTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly string _settingsDir;
    private readonly ServiceProvider _provider;

    public Phase23EmptyInspectChromeTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase23EmptyInspectChrome_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        _settingsDir = Phase23IsolatedSettingsTestSupport.ConfigureIsolatedSettings(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot)));
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        _provider = services.BuildServiceProvider();
        _ = _provider.GetRequiredService<AgentTransparencySettingsSync>();
    }

    [Fact]
    public async Task MemoryPanel_Empty_ShowsMinimalChrome_HidesStandingDenialUntilSubmit()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);

        var memory = new AgentMemoryPanel();
        try
        {
            memory.SetViewModel(management);

            Assert.False(memory.RecordSelector.IsVisible);
            Assert.False(memory.DraftInput.IsVisible);
            Assert.False(memory.CorrectButton.IsVisible);
            Assert.False(memory.DisableButton.IsVisible);
            Assert.False(memory.SubmitDenialCaptionControl.IsVisible);
            Assert.True(string.IsNullOrEmpty(memory.SubmitDenialCaptionControl.Text));
            Assert.True(memory.CreateButtonControl.IsVisible);
            Assert.True(memory.RefreshButton.IsVisible);
            Assert.True(memory.CloseButton.IsVisible);

            memory.CreateButtonControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(memory.DraftInput.IsVisible);
            Assert.False(memory.SubmitDenialCaptionControl.IsVisible);

            memory.CreateButtonControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(memory.SubmitDenialCaptionControl.IsVisible);
            Assert.Contains("Content is required", memory.SubmitDenialCaptionControl.Text, StringComparison.Ordinal);
        }
        finally
        {
            memory.Dispose();
        }
    }

    [Fact]
    public async Task UsagePanel_Empty_ShowsMinimalChrome_HidesRecordSelectorAndCapture()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();
        Assert.Equal(AgentUsageSurfaceState.Empty, management.UsageInspection.SurfaceState);

        var usage = new AgentUsagePanel();
        try
        {
            usage.SetViewModel(management);

            Assert.False(usage.RecordSelector.IsVisible);
            Assert.True(usage.RefreshButton.IsVisible);
            Assert.True(usage.CloseButton.IsVisible);
            Assert.Contains("not zero", usage.SummaryCaptionControl.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            usage.Dispose();
        }
    }

    [Fact]
    public async Task TracePanel_CaptureDisabled_ShowsStatusCloseAndOpenSettingsOnly()
    {
        await Phase23SettingsTestSupport.DisableTraceCaptureAsync(_provider.GetRequiredService<ISettingsService>());

        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenTraceCommand.Execute().Subscribe();
        management.RefreshTracePresentation();
        Assert.False(management.TraceAvailability.CurrentState.CaptureEnabled);

        var trace = new AgentTracePanel();
        try
        {
            trace.SetViewModel(management);

            Assert.Contains("change in Settings", trace.StatusCaptionControl.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.False(trace.SummaryCaptionControl.IsVisible);
            Assert.False(trace.RecordSelector.IsVisible);
            Assert.False(trace.PagingCaptionControl.IsVisible);
            Assert.False(trace.RefreshButton.IsVisible);
            Assert.True(trace.CloseButton.IsVisible);
            Assert.True(trace.OpenSettingsButton.IsVisible);

            var settingsOpened = false;
            trace.OpenSettingsRequested = () => settingsOpened = true;
            trace.OpenSettingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(settingsOpened);
        }
        finally
        {
            trace.Dispose();
        }
    }

    [Fact]
    public async Task MemoryPanel_Unavailable_KeepsFullInspectionChrome()
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

            Assert.True(memory.RecordSelector.IsVisible);
            Assert.True(memory.DraftInput.IsVisible);
            Assert.True(memory.LifecycleActionsControl.IsVisible);
            Assert.True(memory.CorrectButton.IsVisible);
        }
        finally
        {
            memory.Dispose();
        }
    }

    [Fact]
    public async Task UsagePanel_Unavailable_KeepsFullInspectionChrome()
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

            Assert.True(usage.RecordSelector.IsVisible);
            Assert.True(usage.RefreshButton.IsVisible);
        }
        finally
        {
            usage.Dispose();
        }
    }

    [Fact]
    public async Task MemoryPanel_Failed_KeepsFullInspectionChrome()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase23MemoryFailed_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(workspaceRoot)));
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        await using var provider = services.BuildServiceProvider();

        var management = provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);

        var store = provider.GetRequiredService<IAgentDurableRecordStore>();
        if (store is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Failed, management.MemoryInspection.SurfaceState);

        var memory = new AgentMemoryPanel();
        try
        {
            memory.SetViewModel(management);

            Assert.True(memory.RecordSelector.IsVisible);
            Assert.True(memory.DraftInput.IsVisible);
            Assert.True(memory.RetryButton.IsEnabled);
        }
        finally
        {
            memory.Dispose();
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EmptyInspectSurfaces_KeepOperationalActionsReachable()
    {
        await Phase23SettingsTestSupport.DisableTraceCaptureAsync(_provider.GetRequiredService<ISettingsService>());

        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenTraceCommand.Execute().Subscribe();
        management.OpenMemoryCommand.Execute().Subscribe();
        management.OpenUsageCommand.Execute().Subscribe();
        management.RefreshTracePresentation();
        await management.RefreshMemorySurfaceAsync();
        await management.RefreshUsageSurfaceAsync();

        var trace = new AgentTracePanel();
        var memory = new AgentMemoryPanel();
        var usage = new AgentUsagePanel();
        try
        {
            trace.SetViewModel(management);
            memory.SetViewModel(management);
            usage.SetViewModel(management);

            AssertNamedFocusableTabStop(trace.CloseButton, "Close trace panel");
            AssertNamedFocusableTabStop(trace.OpenSettingsButton, "Open application settings");
            AssertNamedFocusableTabStop(memory.CreateButtonControl, "Create durable memory record");
            AssertNamedFocusableTabStop(memory.RefreshButton, "Refresh durable memory");
            AssertNamedFocusableTabStop(memory.CloseButton, "Close memory panel");
            AssertNamedFocusableTabStop(usage.RefreshButton, "Refresh usage evidence");
            AssertNamedFocusableTabStop(usage.CloseButton, "Close usage panel");

            Assert.False(trace.RecordSelector.IsTabStop);
            Assert.False(memory.RecordSelector.IsTabStop);
            Assert.False(usage.RecordSelector.IsTabStop);
        }
        finally
        {
            trace.Dispose();
            memory.Dispose();
            usage.Dispose();
        }
    }

    private static void AssertNamedFocusableTabStop(Control control, string expectedName)
    {
        Assert.Equal(expectedName, AutomationProperties.GetName(control));
        Assert.True(control.Focusable);
        Assert.True(control.IsTabStop);
    }

    public void Dispose()
    {
        _provider.Dispose();
        Phase23IsolatedSettingsTestSupport.TryDeleteDirectory(_settingsDir);
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
