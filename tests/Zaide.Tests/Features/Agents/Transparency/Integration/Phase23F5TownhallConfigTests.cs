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
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents.Transparency.Integration;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 23 F5: Townhall chrome removal, Settings deep-link parity, binding read-only ACP.
/// </summary>
public sealed class Phase23F5TownhallConfigTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly string _settingsDir;
    private readonly ServiceProvider _provider;

    public Phase23F5TownhallConfigTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase23F5TownhallConfig_" + Guid.NewGuid().ToString("N"));
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
    public async Task EmptyTraceAndUsage_DoNotExposeCaptureToggles()
    {
        var settings = _provider.GetRequiredService<ISettingsService>();
        await Phase23SettingsTestSupport.DisableTraceCaptureAsync(settings);
        await Phase23SettingsTestSupport.DisableUsageCaptureAsync(settings);

        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenTraceCommand.Execute().Subscribe();
        management.RefreshTracePresentation();

        var trace = new AgentTracePanel();
        try
        {
            trace.SetViewModel(management);

            Assert.Contains(
                "change in Settings",
                trace.StatusCaptionControl.Text ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(trace.OpenSettingsButton.IsVisible);
        }
        finally
        {
            trace.Dispose();
        }

        management.OpenUsageCommand.Execute().Subscribe();
        await management.RefreshUsageSurfaceAsync();

        var usage = new AgentUsagePanel();
        try
        {
            usage.SetViewModel(management);
            Assert.True(usage.OpenSettingsButton.IsVisible);
            Assert.True(usage.RefreshButton.IsVisible);
        }
        finally
        {
            usage.Dispose();
        }
    }

    [Fact]
    public async Task CaptureDefaults_FollowSettings_NotPanelToggles()
    {
        var settings = _provider.GetRequiredService<ISettingsService>();
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        await Phase23SettingsTestSupport.EnableTraceCaptureAsync(settings);
        management.RefreshTracePresentation();
        Assert.True(management.TraceAvailability.CurrentState.CaptureEnabled);

        await Phase23SettingsTestSupport.DisableTraceCaptureAsync(settings);
        management.RefreshTracePresentation();
        Assert.False(management.TraceAvailability.CurrentState.CaptureEnabled);
    }

    [Fact]
    public async Task TownhallAcpDrafts_SyncFromSettings()
    {
        var settings = _provider.GetRequiredService<ISettingsService>();
        var townhall = _provider.GetRequiredService<TownhallViewModel>();

        await settings.UpdateAsync(current => current with
        {
            Agents = current.Agents with
            {
                AcpExecutablePath = "/usr/bin/fake-agent",
                AcpArguments = "healthy",
                AcpExpectedAgentName = "acp-fake-agent",
                AcpExpectedAgentVersion = "9.9.9",
            },
        });

        Assert.Equal("/usr/bin/fake-agent", townhall.AcpExecutableDraft);
        Assert.Equal("healthy", townhall.AcpArgumentsDraft);
        Assert.Equal("acp-fake-agent", townhall.AcpExpectedNameDraft);
        Assert.Equal("9.9.9", townhall.AcpExpectedVersionDraft);

        var panel = new AgentBackendBindingPanel
        {
            AcpExecutablePath = townhall.AcpExecutableDraft,
            AcpArgumentsText = townhall.AcpArgumentsDraft,
            AcpExpectedAgentName = townhall.AcpExpectedNameDraft,
            AcpExpectedAgentVersion = townhall.AcpExpectedVersionDraft,
        };
        Assert.Equal(
            "Open application settings to edit ACP configuration",
            AutomationProperties.GetName(panel.OpenSettingsButton));
    }

    [Fact]
    public void OpenSettingsAffordances_RouteToMainWindowShowSettings()
    {
        var repoRoot = FindRepositoryRoot();
        var mainWindowSource = File.ReadAllText(Path.Combine(repoRoot, "src", "App", "Shell", "MainWindow.axaml.cs"));
        var townhallSource = File.ReadAllText(Path.Combine(repoRoot, "src", "Features", "Townhall", "Presentation", "TownhallView.cs"));

        Assert.Contains("ViewModel.ShowSettings.Handle", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("OpenSettingsRequested = () =>", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_backendBindingPanel.OpenSettingsRequested +=", townhallSource, StringComparison.Ordinal);
        Assert.Contains("OpenSettingsRequested?.Invoke()", townhallSource, StringComparison.Ordinal);

        var trace = new AgentTracePanel();
        var settingsOpened = false;
        trace.OpenSettingsRequested = () => settingsOpened = true;
        trace.OpenSettingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(settingsOpened);
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
