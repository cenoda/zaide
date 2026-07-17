using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Xunit;
using Zaide;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Editor.Presentation;
using Zaide.App.Composition;

namespace Zaide.Tests.Features.Agents.Presentation;

/// <summary>
/// M5 lifetime/cleanup tests for <see cref="AgentPanelHostView"/>.
///
/// Verifies that host-level (collection/property) and per-panel
/// (<see cref="AgentPanelView.SendRequested"/>) subscriptions detach cleanly,
/// that retained view/tab references release safely, and that rebinding via
/// <see cref="AgentPanelHostView.SetHost"/> does not leave stale subscriptions.
/// Also guards that direct/routed send behavior is unaffected by the cleanup
/// change and that no execution/provider/routing type leaked into the view.
/// </summary>
public class AgentPanelHostViewLifetimeTests
{
    static AgentPanelHostViewLifetimeTests()
    {
        // Initialize ReactiveUI (schedulers etc.) like other view tests.
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();

        EnsureApplication();

        // AgentPanelView is a ReactiveUserControl whose constructor calls
        // WhenActivated, which requires the Avalonia activation fetcher.
        Locator.CurrentMutable.Register(
            () => new AvaloniaActivationForViewFetcher(),
            typeof(IActivationForViewFetcher));
    }

    private static void EnsureApplication()
    {
        if (Avalonia.Application.Current is global::Zaide.App.Composition.App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
            {
                app.Initialize();
            }

            return;
        }

        var createdApp = new global::Zaide.App.Composition.App();
        createdApp.Initialize();
    }

    private static AgentPanelHostView CreateBoundView(out AgentPanelHost host)
    {
        var view = new AgentPanelHostView();
        host = new AgentPanelHost();
        view.SetHost(host);
        return view;
    }

    private static object? GetHostField(AgentPanelHostView view)
    {
        var field = typeof(AgentPanelHostView).GetField(
            "_host", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(view);
    }

    private static int SendSubscriberCount(AgentPanelView view)
    {
        var field = typeof(AgentPanelView).GetField(
            "SendRequested", BindingFlags.NonPublic | BindingFlags.Instance);
        var handler = (Action<string, string>?)field?.GetValue(view);
        return handler?.GetInvocationList().Length ?? 0;
    }

    private static bool HostCollectionSubscribedTo(AgentPanelHost host, AgentPanelHostView view)
    {
        var field = typeof(ObservableCollection<AgentPanelState>).GetField(
            "CollectionChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        var handler = (NotifyCollectionChangedEventHandler?)field?.GetValue(host.Panels);
        return handler is not null && handler.GetInvocationList().Any(d => ReferenceEquals(d.Target, view));
    }

    private static bool HostPropertyChangedSubscribedTo(AgentPanelHost host, AgentPanelHostView view)
    {
        var field = typeof(AgentPanelHost).GetField(
            "PropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        var handler = (PropertyChangedEventHandler?)field?.GetValue(host);
        return handler is not null && handler.GetInvocationList().Any(d => ReferenceEquals(d.Target, view));
    }

    [Fact]
    public void DetachHost_DetachesHostCollectionSubscription()
    {
        var view = CreateBoundView(out var host);
        host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        Assert.Single(view.Panels);

        view.DetachHost();

        // Adding a panel to the host must no longer propagate to the view.
        host.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        Assert.Empty(view.Panels);
        Assert.Null(GetHostField(view));
    }

    [Fact]
    public void DetachHost_DetachesHostPropertyChangedSubscription()
    {
        var view = CreateBoundView(out var host);
        var panel1 = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var panel2 = host.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        Assert.True(HostPropertyChangedSubscribedTo(host, view));

        view.DetachHost();

        Assert.False(HostPropertyChangedSubscribedTo(host, view));
        Assert.Null(GetHostField(view));

        // Activating a panel after detach must not throw or rebind the view.
        host.ActivatePanel(panel1.PanelId);
        Assert.Empty(view.Panels);
    }

    [Fact]
    public void DetachHost_DetachesPerPanelSendSubscriptions()
    {
        var view = CreateBoundView(out var host);
        var panel = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var retainedView = view.Panels[panel];
        Assert.Equal(1, SendSubscriberCount(retainedView));

        view.DetachHost();

        Assert.Equal(0, SendSubscriberCount(retainedView));
    }

    [Fact]
    public void SetHost_RebindingDoesNotLeaveStaleSubscriptions()
    {
        var view = new AgentPanelHostView();
        var hostA = new AgentPanelHost();
        view.SetHost(hostA);
        var panelA = hostA.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var retainedA = view.Panels[panelA];

        Assert.True(HostCollectionSubscribedTo(hostA, view));
        Assert.True(HostPropertyChangedSubscribedTo(hostA, view));
        Assert.Equal(1, SendSubscriberCount(retainedA));

        var hostB = new AgentPanelHost();
        view.SetHost(hostB);

        // Old host's subscriptions are gone.
        Assert.False(HostCollectionSubscribedTo(hostA, view));
        Assert.False(HostPropertyChangedSubscribedTo(hostA, view));
        // Old panel's per-panel send subscription is gone.
        Assert.Equal(0, SendSubscriberCount(retainedA));
        // New host is bound and functional.
        Assert.True(HostCollectionSubscribedTo(hostB, view));
        var panelB = hostB.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        Assert.Single(view.Panels);
        Assert.Same(panelB, view.Panels.Keys.Single());
    }

    [Fact]
    public void DetachHost_ClearsRetainedPanelAndTabReferences()
    {
        var view = CreateBoundView(out var host);
        host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        host.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        Assert.Equal(2, view.Panels.Count);
        Assert.Equal(2, view.TabItems.Count);

        view.DetachHost();

        Assert.Empty(view.Panels);
        Assert.Empty(view.TabItems);
    }

    [Fact]
    public void DetachHost_ReleasesRetainedViewModels()
    {
        var view = CreateBoundView(out var host);
        var panel = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var retainedView = view.Panels[panel];
        Assert.NotNull(retainedView.ViewModel);

        view.DetachHost();

        Assert.Null(retainedView.ViewModel);
    }

    [Fact]
    public void DetachHost_SafeWithZeroPanels()
    {
        var view = CreateBoundView(out var host);
        Assert.Empty(view.Panels);

        var ex = Record.Exception(() => view.DetachHost());

        Assert.Null(ex);
        Assert.Empty(view.Panels);
        Assert.Null(GetHostField(view));
    }

    [Fact]
    public void DetachHost_SafeWhenCalledMultipleTimes()
    {
        var view = CreateBoundView(out var host);
        host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");

        var ex = Record.Exception(() =>
        {
            view.DetachHost();
            view.DetachHost();
        });

        Assert.Null(ex);
        Assert.Empty(view.Panels);
        Assert.Null(GetHostField(view));
    }

    [Fact]
    public void SetHost_PanelSendStillBubblesToHostView()
    {
        var view = CreateBoundView(out var host);
        var panel = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var retainedView = view.Panels[panel];

        string? receivedPanelId = null;
        string? receivedMessage = null;
        view.PanelSendRequested += (id, msg) =>
        {
            receivedPanelId = id;
            receivedMessage = msg;
        };

        // Simulate a send from the retained panel view (direct-send path).
        var sendField = typeof(AgentPanelView).GetField(
            "SendRequested", BindingFlags.NonPublic | BindingFlags.Instance);
        var handler = (Action<string, string>?)sendField!.GetValue(retainedView);
        handler!.Invoke(panel.PanelId, "hello world");

        Assert.Equal(panel.PanelId, receivedPanelId);
        Assert.Equal("hello world", receivedMessage);
    }

    [Fact]
    public void SetHost_RebindKeepsSendBubbling()
    {
        var view = new AgentPanelHostView();
        var hostA = new AgentPanelHost();
        view.SetHost(hostA);
        hostA.CreatePanel("agent-1", "Alpha", "Icon.Avatar");

        var hostB = new AgentPanelHost();
        view.SetHost(hostB);
        var panelB = hostB.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        var retainedB = view.Panels[panelB];

        string? received = null;
        view.PanelSendRequested += (id, msg) => received = $"{id}:{msg}";

        var sendField = typeof(AgentPanelView).GetField(
            "SendRequested", BindingFlags.NonPublic | BindingFlags.Instance);
        var handler = (Action<string, string>?)sendField!.GetValue(retainedB);
        handler!.Invoke(panelB.PanelId, "routed");

        Assert.Equal($"{panelB.PanelId}:routed", received);
    }

    [Fact]
    public void AgentPanelHostView_DoesNotReferenceExecutionOrProviderTypes()
    {
        var t = typeof(AgentPanelHostView);
        var forbidden = new[]
        {
            "IAgentExecutionService", "AgentExecutionService",
            "IAgentExecutionCoordinator", "AgentExecutionCoordinator",
            "IAgentRouter", "AgentRouter", "MentionParser",
            "TownhallViewModel"
        };

        var memberTypes = t
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.FieldType)
            .Concat(t.GetProperties().Select(p => p.PropertyType));

        foreach (var mt in memberTypes)
        {
            var fullName = mt.FullName ?? mt.Name;
            foreach (var name in forbidden)
            {
                Assert.False(fullName.Contains(name),
                    $"AgentPanelHostView member references forbidden type: {name}");
            }
        }
    }

    private static Button? FindCloseButton(Border tabItem)
    {
        if (tabItem.Child is not Grid grid)
            return null;

        return grid.Children.OfType<Button>()
            .FirstOrDefault(b => Equals(b.Tag, "AgentTabClose"));
    }

    [Fact]
    public void TabItem_HasCloseButtonWithKeyboardAffordance()
    {
        var view = CreateBoundView(out var host);
        var panel = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");

        Assert.True(view.TabItems.TryGetValue(panel, out var tabItem));
        var close = FindCloseButton(tabItem!);
        Assert.NotNull(close);
        Assert.True(close!.Focusable);
        Assert.Equal("Close agent tab", Avalonia.Automation.AutomationProperties.GetName(close));
    }

    [Fact]
    public void CloseButton_RemovesOnlyThatTabFromView()
    {
        var view = CreateBoundView(out var host);
        var panel1 = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var panel2 = host.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        var panel3 = host.CreatePanel("agent-3", "Gamma", "Icon.Avatar");

        var close = FindCloseButton(view.TabItems[panel2]);
        Assert.NotNull(close);
        close!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(2, view.Panels.Count);
        Assert.False(view.Panels.ContainsKey(panel2));
        Assert.True(view.Panels.ContainsKey(panel1));
        Assert.True(view.Panels.ContainsKey(panel3));
        Assert.Equal(2, view.TabItems.Count);
        Assert.DoesNotContain(panel2, host.Panels);
    }

    [Fact]
    public void CloseButton_OnActiveTab_SelectsNeighborWithoutStoppingLifecycle()
    {
        var view = CreateBoundView(out var host);
        var panel1 = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");
        var panel2 = host.CreatePanel("agent-2", "Beta", "Icon.Avatar");
        // panel2 is active
        panel2.Status = "Thinking";
        panel2.IsBusy = true;
        panel2.OutputHistory.Add("still running");

        var close = FindCloseButton(view.TabItems[panel2]);
        Assert.NotNull(close);
        close!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Same(panel1, host.ActivePanel);
        Assert.Single(view.Panels);
        Assert.Equal("Thinking", panel2.Status);
        Assert.True(panel2.IsBusy);
        Assert.Equal(new[] { "still running" }, panel2.OutputHistory.ToArray());
    }

    [Fact]
    public void CloseButton_FinalTab_YieldsEmptyViewState()
    {
        var view = CreateBoundView(out var host);
        var panel = host.CreatePanel("agent-1", "Alpha", "Icon.Avatar");

        var close = FindCloseButton(view.TabItems[panel]);
        Assert.NotNull(close);
        close!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Empty(host.Panels);
        Assert.Null(host.ActivePanel);
        Assert.Empty(view.Panels);
        Assert.Empty(view.TabItems);
    }
}
