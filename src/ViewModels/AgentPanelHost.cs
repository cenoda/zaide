using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
public sealed class AgentPanelHost : IAgentPanelHost, INotifyPropertyChanged
{
    private readonly ObservableCollection<AgentPanelState> _panels;
    private AgentPanelState? _activePanel;

    public ObservableCollection<AgentPanelState> Panels => _panels;

    public AgentPanelState? ActivePanel => _activePanel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentPanelHost()
    {
        _panels = new ObservableCollection<AgentPanelState>();
    }

    private static readonly (string AgentId, string AgentName, string AvatarResourceKey)[] _seedIdentities =
    {
        ("alpha", "Alpha", "Icon.Avatar"),
        ("beta", "Beta", "Icon.Avatar"),
        ("gamma", "Gamma", "Icon.Avatar"),
        ("delta", "Delta", "Icon.Avatar"),
    };
    private int _nextSeedIndex;

    /// <summary>
    /// Creates a new agent panel using the next seeded identity from the
    /// built-in list. Falls back to sequential naming if the seeded list
    /// is exhausted.
    /// </summary>
    public AgentPanelState CreatePanel()
    {
        string agentId, agentName, avatar;
        if (_nextSeedIndex < _seedIdentities.Length)
        {
            var seed = _seedIdentities[_nextSeedIndex];
            agentId = seed.AgentId;
            agentName = seed.AgentName;
            avatar = seed.AvatarResourceKey;
            _nextSeedIndex++;
        }
        else
        {
            int n = _nextSeedIndex - _seedIdentities.Length + 1;
            agentId = $"agent-{n}";
            agentName = $"Agent {n}";
            avatar = "Icon.Avatar";
            _nextSeedIndex++;
        }

        return CreatePanel(agentId, agentName, avatar);
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

        _activePanel = panel;
        _panels.Add(panel);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActivePanel)));

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

        // Same-panel activation is a no-op.
        if (panel == _activePanel)
            return;

        _activePanel = panel;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActivePanel)));
    }
}
