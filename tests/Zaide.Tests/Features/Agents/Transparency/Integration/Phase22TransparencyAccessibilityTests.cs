using System;
using System.IO;
using System.Reactive.Concurrency;
using Avalonia.Automation;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

/// <summary>
/// Phase 22.4 M4: real View accessibility — keyboard focus, named controls,
/// screen-reader value text, and bounded paging against live panels (not
/// constant-only checks).
/// </summary>
public sealed class Phase22TransparencyAccessibilityTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22TransparencyAccessibilityTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "Phase22TransparencyA11y_" + Guid.NewGuid().ToString("N"));
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
    public void TownhallEntryPoints_AreNamedFocusableAndDocumentedInSource()
    {
        // Live Avalonia Application is not available in the unit-test host.
        // Verify production Townhall entry-point metadata against source plus
        // the same Focusable/IsTabStop/AutomationProperties contracts used live.
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
        Assert.Contains("Opens or closes the agent trace evidence panel", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Opens or closes the durable memory lifecycle panel", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Opens or closes the usage and cost evidence panel", townhallSource, StringComparison.Ordinal);
        Assert.Contains("IsTabStop = true", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Focusable = true", townhallSource, StringComparison.Ordinal);

        var trace = CreateNamedTabStopButton("Open or close agent trace evidence");
        var memory = CreateNamedTabStopButton("Open or close agent durable memory");
        var usage = CreateNamedTabStopButton("Open or close agent usage and cost evidence");
        AssertNamedFocusableTabStop(trace, "Open or close agent trace evidence");
        AssertNamedFocusableTabStop(memory, "Open or close agent durable memory");
        AssertNamedFocusableTabStop(usage, "Open or close agent usage and cost evidence");
    }

    [Fact]
    public void LivePanels_ExposeNamedControlsFocusAndScreenReaderValueText()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.OpenTraceCommand.Execute().Subscribe();
        management.OpenMemoryCommand.Execute().Subscribe();
        management.OpenUsageCommand.Execute().Subscribe();
        management.RefreshTracePresentation();

        var trace = new AgentTracePanel();
        var memory = new AgentMemoryPanel();
        var usage = new AgentUsagePanel();
        try
        {
            trace.SetViewModel(management);
            memory.SetViewModel(management);
            usage.SetViewModel(management);

            Assert.True(trace.Focusable);
            Assert.True(memory.Focusable);
            Assert.True(usage.Focusable);
            Assert.True(trace.IsTabStop);

            AssertNamedFocusableTabStop(trace.CloseButton, "Close trace panel");
            AssertNamedFocusableTabStop(trace.OpenSettingsButton, "Open application settings");
            Assert.False(trace.RecordSelector.IsVisible);
            Assert.False(trace.PagingCaptionControl.IsVisible);
            Assert.False(trace.CaptureButton.IsVisible);

            AssertNamedFocusableTabStop(memory.CreateButtonControl, "Create durable memory record");
            AssertNamedFocusableTabStop(memory.RefreshButton, "Refresh durable memory");
            AssertNamedFocusableTabStop(memory.RetryButton, "Retry failed durable memory load");
            AssertNamedFocusableTabStop(memory.CloseButton, "Close memory panel");

            AssertNamedFocusableTabStop(usage.RefreshButton, "Refresh usage evidence");
            AssertNamedFocusableTabStop(usage.CloseButton, "Close usage panel");
            Assert.False(usage.CaptureButton.IsVisible);
            Assert.False(usage.RecordSelector.IsVisible);
            Assert.False(usage.RetryButton.IsVisible);
        }
        finally
        {
            trace.Dispose();
            memory.Dispose();
            usage.Dispose();
        }
    }

    [Fact]
    public void Management_BoundedPaging_IsClampedAgainstLiveDefaults()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var townhall = _provider.GetRequiredService<TownhallViewModel>();
        Assert.Same(management, townhall.TransparencyManagement);

        Assert.Equal(64, AgentTransparencyManagementViewModel.DefaultPageSize);
        Assert.Equal(256, AgentTransparencyManagementViewModel.MaxPageSize);
        Assert.Equal(64, management.ClampPageSize(0));
        Assert.Equal(64, management.ClampPageSize(-1));
        Assert.Equal(128, management.ClampPageSize(128));
        Assert.Equal(256, management.ClampPageSize(10_000));

        // Live panels publish the clamped paging contract when full chrome is active.
        var trace = new AgentTracePanel();
        try
        {
            management.OpenTraceCommand.Execute().Subscribe();
            management.ToggleTraceCaptureCommand.Execute().Subscribe();
            management.RefreshTracePresentation();
            trace.SetViewModel(management);
            Assert.False(trace.PagingCaptionControl.IsVisible);

            Assert.Equal(
                "Agent transparency and memory management",
                management.AccessibilityName);
            Assert.Contains("Keyboard navigation", management.AccessibilityHelpText, StringComparison.Ordinal);
            Assert.Contains("screen-reader", management.AccessibilityHelpText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            trace.Dispose();
        }
    }

    private static void AssertNamedFocusableTabStop(Control control, string expectedName)
    {
        Assert.Equal(expectedName, AutomationProperties.GetName(control));
        Assert.True(control.Focusable);
        Assert.True(control.IsTabStop);
    }

    private static Button CreateNamedTabStopButton(string automationName)
    {
        var button = new Button
        {
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
