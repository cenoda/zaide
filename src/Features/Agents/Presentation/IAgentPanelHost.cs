using System.Collections.ObjectModel;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Host seam for agent panel collection and active-panel selection.
///
/// Mirrors the ITerminalHost pattern: the host owns the panel collection
/// and active selection; MainWindowViewModel composes the host rather than
/// owning panel state directly.
///
/// Phase 5.1.2 only — intentionally narrow. No UI, execution, Townhall,
/// routing, or persistence concerns.
/// </summary>
public interface IAgentPanelHost
{
    /// <summary>
    /// Observable collection of all agent panels owned by this host.
    /// </summary>
    ObservableCollection<AgentPanelState> Panels { get; }

    /// <summary>
    /// The currently active agent panel, or null if no panels exist.
    /// </summary>
    AgentPanelState? ActivePanel { get; }

    /// <summary>
    /// Create a new agent panel using the next seeded identity.
    /// The new panel becomes the active panel.
    /// </summary>
    AgentPanelState CreatePanel();

    /// <summary>
    /// Create a new agent panel with the given agent identity and add it
    /// to the collection. The new panel becomes the active panel.
    /// </summary>
    AgentPanelState CreatePanel(string agentId, string agentName, string avatarResourceKey);

    /// <summary>
    /// Create a new agent panel for a canonical catalog actor and add it to
    /// the collection. The new panel becomes the active panel.
    /// </summary>
    AgentPanelState CreatePanelForActor(ActorId actorId);

    /// <summary>
    /// Activate the panel identified by <paramref name="panelId"/>.
    /// If the panel does not exist, this is a no-op.
    /// </summary>
    void ActivatePanel(string panelId);

    /// <summary>
    /// Remove the panel identified by <paramref name="panelId"/> from the
    /// Agents UI collection only. Does not stop, cancel, terminate, or
    /// otherwise alter the agent's running lifecycle; panel state (including
    /// output history) is left intact on the removed model. If the closed
    /// panel was active, a neighboring panel becomes active when any remain.
    /// </summary>
    void ClosePanel(string panelId);
}
