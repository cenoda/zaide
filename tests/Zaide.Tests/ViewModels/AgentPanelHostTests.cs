using System.Collections.ObjectModel;
using System.Linq;
using Xunit;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

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
}
