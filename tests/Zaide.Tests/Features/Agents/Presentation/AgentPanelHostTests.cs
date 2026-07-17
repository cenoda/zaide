using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.Features.Agents.Presentation;

/// <summary>
/// Focused tests for the agent-panel host seam.
///
/// Phase 5.1.2 only — covers panel collection ownership, active-panel
/// selection, switching, and missing/reset behavior. Does not reference
/// UI, execution, Townhall, routing, or persistence concerns.
/// </summary>
public class AgentPanelHostTests
{
    private static AgentPanelHost CreateHost() => new();

    [Fact]
    public void Host_StartsWithEmptyPanelCollection()
    {
        var host = CreateHost();
        Assert.Empty(host.Panels);
        Assert.Null(host.ActivePanel);
    }

    [Fact]
    public void CreatePanel_AddsPanelToCollection()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        Assert.Single(host.Panels);
        Assert.Contains(panel, host.Panels);
    }

    [Fact]
    public void CreatePanel_SetsPanelAsActive()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        Assert.Same(panel, host.ActivePanel);
    }

    [Fact]
    public void CreatePanel_RaisesActivePanelPropertyChanged()
    {
        var host = CreateHost();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)host).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        Assert.Same(panel, host.ActivePanel);
        Assert.Contains(nameof(IAgentPanelHost.ActivePanel), changed);
    }

    [Fact]
    public void CreatePanel_AssignsUniquePanelIds()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");

        Assert.NotEqual(panel1.PanelId, panel2.PanelId);
    }

    [Fact]
    public void CreatePanel_SetsProvidedAgentIdentity()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.Equal("agent-x", panel.AgentId);
        Assert.Equal("X Agent", panel.AgentName);
        Assert.Equal("avatar_x", panel.AvatarResourceKey);
    }

    [Fact]
    public void CreatePanel_InitializesStatusToIdle()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        Assert.Equal("Idle", panel.Status);
    }

    [Fact]
    public void CreatePanel_InitializesDraftInputToEmpty()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        Assert.Equal(string.Empty, panel.DraftInput);
    }

    [Fact]
    public void CreatePanel_NewPanelBecomesActive()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");

        Assert.Same(panel2, host.ActivePanel);
        Assert.Equal(2, host.Panels.Count);
    }

    [Fact]
    public void ActivatePanel_SwitchesActivePanel()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");

        host.ActivatePanel(panel1.PanelId);

        Assert.Same(panel1, host.ActivePanel);
    }

    [Fact]
    public void ActivatePanel_RaisesActivePanelPropertyChanged()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var changed = new List<string?>();
        ((INotifyPropertyChanged)host).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        host.ActivatePanel(panel1.PanelId);

        Assert.Same(panel1, host.ActivePanel);
        Assert.Contains(nameof(IAgentPanelHost.ActivePanel), changed);
    }

    [Fact]
    public void ActivatePanel_WithSamePanel_IsNoOp()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ActivatePanel(panel.PanelId);

        Assert.Same(panel, host.ActivePanel);
        Assert.Single(host.Panels);
    }

    [Fact]
    public void ActivatePanel_WithNonExistentId_IsNoOp()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ActivatePanel("non-existent-id");

        Assert.Same(panel, host.ActivePanel);
        Assert.Single(host.Panels);
    }

    [Fact]
    public void ActivatePanel_WithEmptyString_IsNoOp()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ActivatePanel(string.Empty);

        Assert.Same(panel, host.ActivePanel);
    }

    [Fact]
    public void ActivatePanel_WithNullString_IsNoOp()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ActivatePanel(null!);

        Assert.Same(panel, host.ActivePanel);
    }

    [Fact]
    public void MultiplePanels_AllPresentInCollection()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var panel3 = host.CreatePanel("agent-3", "Gamma", "avatar_gamma");

        Assert.Equal(3, host.Panels.Count);
        Assert.Contains(panel1, host.Panels);
        Assert.Contains(panel2, host.Panels);
        Assert.Contains(panel3, host.Panels);
    }

    [Fact]
    public void SwitchBackAndForth_PreservesPanelIdentity()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");

        // Switch to panel1
        host.ActivatePanel(panel1.PanelId);
        Assert.Same(panel1, host.ActivePanel);

        // Switch back to panel2
        host.ActivatePanel(panel2.PanelId);
        Assert.Same(panel2, host.ActivePanel);

        // Switch to panel1 again
        host.ActivatePanel(panel1.PanelId);
        Assert.Same(panel1, host.ActivePanel);
    }

    [Fact]
    public void ActivatePanel_WithSamePanel_MultiPanel_IsNoOp()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");

        // Switch to panel1
        host.ActivatePanel(panel1.PanelId);
        Assert.Same(panel1, host.ActivePanel);

        // Same-panel activation: panel1 stays active
        host.ActivatePanel(panel1.PanelId);
        Assert.Same(panel1, host.ActivePanel);

        // Switch to panel2
        host.ActivatePanel(panel2.PanelId);
        Assert.Same(panel2, host.ActivePanel);

        // Same-panel activation: panel2 stays active
        host.ActivatePanel(panel2.PanelId);
        Assert.Same(panel2, host.ActivePanel);
    }

    [Fact]
    public void CreatePanel_AfterActivateNonExistent_StillWorks()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        // ActivatePanel with non-existent ID should not corrupt state
        host.ActivatePanel("ghost");
        Assert.Same(panel1, host.ActivePanel);

        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        Assert.Same(panel2, host.ActivePanel);
        Assert.Equal(2, host.Panels.Count);
    }

    [Fact]
    public void Host_ReferencesNoUIOrExecutionOrTownhallTypes()
    {
        // Verify the host type's API surface is purely model-based and does
        // not reference UI, execution, or Townhall types.
        var hostType = typeof(AgentPanelHost);

        // Get all fields and properties
        var propertyTypes = hostType.GetProperties().Select(p => p.PropertyType);
        var fieldTypes = hostType.GetFields().Select(f => f.FieldType);

        foreach (var t in propertyTypes.Concat(fieldTypes))
        {
            var name = t.FullName ?? t.Name;

            // Allowed: AgentPanelState, ObservableCollection<T>, string, void
            if (t == typeof(AgentPanelState)) continue;
            if (t == typeof(System.Collections.ObjectModel.ObservableCollection<AgentPanelState>)) continue;
            if (name.StartsWith("System.")) continue;

            Assert.Fail($"Unexpected type reference in host API: {name}");
        }
    }

    [Fact]
    public void CreatePanel_Parameterless_AssignsSeededDistinctIdentities()
    {
        var host = CreateHost();
        var p1 = host.CreatePanel();
        var p2 = host.CreatePanel();
        var p3 = host.CreatePanel();
        var p4 = host.CreatePanel();

        Assert.Equal("alpha", p1.AgentId);
        Assert.Equal("Alpha", p1.AgentName);
        Assert.Equal("Icon.Avatar", p1.AvatarResourceKey);

        Assert.Equal("beta", p2.AgentId);
        Assert.Equal("Beta", p2.AgentName);

        Assert.Equal("gamma", p3.AgentId);
        Assert.Equal("Gamma", p3.AgentName);

        Assert.Equal("delta", p4.AgentId);
        Assert.Equal("Delta", p4.AgentName);

        Assert.All(new[] { p1, p2, p3, p4 }, p =>
        {
            Assert.False(string.IsNullOrEmpty(p.AgentId));
            Assert.False(string.IsNullOrEmpty(p.AgentName));
            Assert.False(string.IsNullOrEmpty(p.AvatarResourceKey));
        });
    }

    [Fact]
    public void CreatePanel_Parameterless_UsesFallbackAfterSeedExhaustion()
    {
        var host = CreateHost();
        for (int i = 0; i < 4; i++) host.CreatePanel();
        var p5 = host.CreatePanel();
        Assert.Equal("agent-1", p5.AgentId);
        Assert.Equal("Agent 1", p5.AgentName);
        Assert.Equal("Icon.Avatar", p5.AvatarResourceKey);
    }

    [Fact]
    public void ClosePanel_RemovesOnlyThatPanel()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var panel3 = host.CreatePanel("agent-3", "Gamma", "avatar_gamma");

        host.ClosePanel(panel2.PanelId);

        Assert.Equal(2, host.Panels.Count);
        Assert.Contains(panel1, host.Panels);
        Assert.DoesNotContain(panel2, host.Panels);
        Assert.Contains(panel3, host.Panels);
    }

    [Fact]
    public void ClosePanel_ActiveMiddle_SelectsNeighborAtSameIndex()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var panel3 = host.CreatePanel("agent-3", "Gamma", "avatar_gamma");
        host.ActivatePanel(panel2.PanelId);

        host.ClosePanel(panel2.PanelId);

        // Index 1 after removal is former panel3.
        Assert.Same(panel3, host.ActivePanel);
        Assert.Equal(2, host.Panels.Count);
    }

    [Fact]
    public void ClosePanel_ActiveLast_SelectsPreviousNeighbor()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        // panel2 is active (CreatePanel activates the new panel)
        Assert.Same(panel2, host.ActivePanel);

        host.ClosePanel(panel2.PanelId);

        Assert.Same(panel1, host.ActivePanel);
        Assert.Single(host.Panels);
    }

    [Fact]
    public void ClosePanel_Inactive_DoesNotChangeActive()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        Assert.Same(panel2, host.ActivePanel);

        host.ClosePanel(panel1.PanelId);

        Assert.Same(panel2, host.ActivePanel);
        Assert.Single(host.Panels);
        Assert.DoesNotContain(panel1, host.Panels);
    }

    [Fact]
    public void ClosePanel_FinalPanel_YieldsEmptyState()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ClosePanel(panel.PanelId);

        Assert.Empty(host.Panels);
        Assert.Null(host.ActivePanel);
    }

    [Fact]
    public void ClosePanel_RaisesActivePanelPropertyChanged_WhenActiveClosed()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var changed = new List<string?>();
        ((INotifyPropertyChanged)host).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        host.ClosePanel(panel2.PanelId);

        Assert.Same(panel1, host.ActivePanel);
        Assert.Contains(nameof(IAgentPanelHost.ActivePanel), changed);
    }

    [Fact]
    public void ClosePanel_DoesNotRaiseActivePanel_WhenInactiveClosed()
    {
        var host = CreateHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var changed = new List<string?>();
        ((INotifyPropertyChanged)host).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        host.ClosePanel(panel1.PanelId);

        Assert.DoesNotContain(nameof(IAgentPanelHost.ActivePanel), changed);
    }

    [Fact]
    public void ClosePanel_DoesNotAlterLifecycleOrHistory()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        panel.Status = "Thinking";
        panel.IsBusy = true;
        panel.OutputHistory.Add("User: still running");
        panel.DraftInput = "partial draft";

        host.ClosePanel(panel.PanelId);

        // UI collection no longer contains the panel, but the model is
        // untouched — close must not stop/cancel or wipe history/state.
        Assert.Empty(host.Panels);
        Assert.Equal("Thinking", panel.Status);
        Assert.True(panel.IsBusy);
        Assert.Equal(new[] { "User: still running" }, panel.OutputHistory.ToArray());
        Assert.Equal("partial draft", panel.DraftInput);
    }

    [Fact]
    public void ClosePanel_WithNonExistentId_IsNoOp()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ClosePanel("missing-id");

        Assert.Same(panel, host.ActivePanel);
        Assert.Single(host.Panels);
    }

    [Fact]
    public void ClosePanel_WithEmptyString_IsNoOp()
    {
        var host = CreateHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        host.ClosePanel(string.Empty);

        Assert.Same(panel, host.ActivePanel);
        Assert.Single(host.Panels);
    }
}
