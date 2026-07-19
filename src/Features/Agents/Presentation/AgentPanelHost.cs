using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Presentation;

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
    private readonly IActorCatalog _actorCatalog;
    private readonly ObservableCollection<AgentPanelState> _panels;
    private AgentPanelState? _activePanel;
    private int _nextSeedIndex;

    public ObservableCollection<AgentPanelState> Panels => _panels;

    public AgentPanelState? ActivePanel => _activePanel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentPanelHost(IActorCatalog actorCatalog)
    {
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _panels = new ObservableCollection<AgentPanelState>();
    }

    /// <summary>
    /// Creates a new agent panel using the next seeded identity from the
    /// built-in list. Falls back to sequential naming if the seeded list
    /// is exhausted.
    /// </summary>
    public AgentPanelState CreatePanel()
    {
        Actor actor;
        if (_nextSeedIndex < _actorCatalog.PanelSeedCount)
        {
            actor = _actorCatalog.GetPanelSeedActor(_nextSeedIndex);
            _nextSeedIndex++;
        }
        else
        {
            int fallbackNumber = _nextSeedIndex - _actorCatalog.PanelSeedCount + 1;
            actor = _actorCatalog.GetOrRegisterPanelFallbackActor(fallbackNumber);
            _nextSeedIndex++;
        }

        return CreatePanelFromActor(actor);
    }

    /// <summary>
    /// Creates a new agent panel with a generated unique PanelId and the
    /// specified agent identity, adds it to the collection, and sets it
    /// as the active panel.
    /// </summary>
    public AgentPanelState CreatePanel(string agentId, string agentName, string avatarResourceKey)
    {
        var actor = _actorCatalog.RegisterOrGetCustomPanelActor(
            agentId,
            agentName,
            avatarResourceKey);
        return CreatePanelFromActor(actor);
    }

    private AgentPanelState CreatePanelFromActor(Actor actor)
    {
        var panel = new AgentPanelState(actor)
        {
            PanelId = Guid.NewGuid().ToString("N"),
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

    /// <summary>
    /// Removes the panel with the specified PanelId from the host collection.
    /// UI-only: does not cancel, stop, or mutate agent lifecycle fields
    /// (Status, IsBusy, OutputHistory, DraftInput). If the closed panel was
    /// active, selects the neighbor at the same index (or the previous panel
    /// when the closed panel was last). Closing the final panel yields the
    /// empty host state (ActivePanel = null).
    /// </summary>
    public void ClosePanel(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
            return;

        var panel = _panels.FirstOrDefault(p => p.PanelId == panelId);
        if (panel is null)
            return;

        var wasActive = panel == _activePanel;
        int index = _panels.IndexOf(panel);

        _panels.Remove(panel);

        if (_panels.Count == 0)
        {
            if (wasActive)
            {
                _activePanel = null;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActivePanel)));
            }

            return;
        }

        if (wasActive)
        {
            int fallbackIndex = index < _panels.Count ? index : _panels.Count - 1;
            _activePanel = _panels[fallbackIndex];
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActivePanel)));
        }
    }
}
