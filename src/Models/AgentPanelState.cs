using System.Collections.ObjectModel;

namespace Zaide.Models;

/// <summary>
/// Minimal state shape for a single agent panel.
/// Phase 5.1.1 only — intentionally narrow.
///
/// Contains no routing metadata, no provider-platform abstractions,
/// and no speculative persistence fields. Multi-panel collection/selection
/// belongs in the future host seam, not here.
/// </summary>
public class AgentPanelState
{
    /// <summary>
    /// Unique identifier for this panel instance.
    /// </summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier for the agent this panel represents.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the agent.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Resource key used to look up the agent's avatar icon.
    /// </summary>
    public string AvatarResourceKey { get; set; } = string.Empty;

    /// <summary>
    /// Current status of this agent panel (e.g. Idle, Thinking, Error).
    /// </summary>
    public string Status { get; set; } = "Idle";

    /// <summary>
    /// Ordered output history for this panel.
    /// Each entry is a free-form text segment (user message, agent reply, status update).
    /// </summary>
    public ObservableCollection<string> OutputHistory { get; } = new();

    /// <summary>
    /// Current draft text being composed by the user for this panel.
    /// </summary>
    public string DraftInput { get; set; } = string.Empty;
}
