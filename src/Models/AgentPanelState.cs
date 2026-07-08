using System.Collections.ObjectModel;
using ReactiveUI;

namespace Zaide.Models;

/// <summary>
/// Minimal state shape for a single agent panel.
/// Phase 5.1.1 — intentionally narrow.
/// M2: Made reactive for coordinator-mutated scalar properties (Status, DraftInput).
/// OutputHistory stays as ObservableCollection<string>.
///
/// Contains no routing metadata, no provider-platform abstractions,
/// and no speculative persistence fields. Multi-panel collection/selection
/// belongs in the future host seam, not here.
/// </summary>
public class AgentPanelState : ReactiveObject
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
    /// Reactive property — mutations through the coordinator are visible to bindings.
    /// </summary>
    private string _status = "Idle";
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    /// <summary>
    /// Ordered output history for this panel.
    /// Each entry is a free-form text segment (user message, agent reply, status update).
    /// ObservableCollection already provides change notifications — no reactive conversion needed.
    /// </summary>
    public ObservableCollection<string> OutputHistory { get; } = new();

    /// <summary>
    /// Current draft text being composed by the user for this panel.
    /// Reactive property — mutations through the coordinator are visible to bindings.
    /// </summary>
    private string _draftInput = string.Empty;
    public string DraftInput
    {
        get => _draftInput;
        set => this.RaiseAndSetIfChanged(ref _draftInput, value);
    }
}