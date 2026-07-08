using System;
using System.Collections.ObjectModel;
using System.Linq;
using Zaide.Models;

namespace Zaide.ViewModels;

/// <summary>
/// Concrete host for agent panel collection and active-panel selection.
///
/// Owns the panel collection and active selection directly. Does not
/// reference UI, execution, Townhall, routing, or persistence concerns.
///
/// Phase 5.1.2 only — intentionally narrow.
/// </summary>
public sealed class AgentPanelHost : IAgentPanelHost
{
    private readonly ObservableCollection<AgentPanelState> _panels;
    private AgentPanelState? _activePanel;

    public ObservableCollection<AgentPanelState> Panels => _panels;

    public AgentPanelState? ActivePanel => _activePanel;

    public AgentPanelHost()
    {
        _panels = new ObservableCollection<AgentPanelState>();
    }

    /// <summary>
    /// Creates a new agent panel with a generated unique PanelId and the
    /// specified agent identity, adds it to the collection, and sets it
    /// as the active panel.
    /// </summary>
    public AgentPanelState CreatePanel(string agentId, string agentName, string avatarResourceKey)
    {
        var panel = new AgentPanelState
        {
            PanelId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            AgentName = agentName,
            AvatarResourceKey = avatarResourceKey,
            Status = "Idle",
            DraftInput = string.Empty
        };

        _panels.Add(panel);
        _activePanel = panel;

        return panel;
    }

    /// <summary>
    /// Activates the panel with the specified PanelId.
    /// If no panel with that id exists, this is a no-op.
    /// If the panel is already active, this is a no-op.
    /// </summary>
    public void ActivatePanel(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
            return;

        var panel = _panels.FirstOrDefault(p => p.PanelId == panelId);
        if (panel is null)
            return;

        _activePanel = panel;
    }
}
