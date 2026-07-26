using System;
using System.Collections.ObjectModel;
using ReactiveUI;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Minimal state shape for a single agent panel.
/// Phase 5.1.1 — intentionally narrow.
/// M2: Made reactive for coordinator-mutated scalar properties (Status, DraftInput).
/// M5b: OutputHistory is a read-only projection of authoritative conversation entries.
///
/// Agent identity projections are read-only views of the canonical <see cref="Actor"/>
/// row supplied at construction. Presentation fields (PanelId, Status, draft, output)
/// remain mutable; identity strings are not independently owned copies.
/// </summary>
public class AgentPanelState : ReactiveObject
{
    private readonly Actor _actor;

    /// <summary>
    /// Creates panel state bound to a canonical actor row and its provisioned
    /// direct conversation.
    /// </summary>
    public AgentPanelState(
        Actor actor,
        ConversationId conversationId,
        ReadOnlyObservableCollection<string> outputHistory)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        if (conversationId == default)
        {
            throw new ArgumentException(
                "Panel state requires a provisioned conversation id.",
                nameof(conversationId));
        }

        ConversationId = conversationId;
        OutputHistory = outputHistory
            ?? throw new ArgumentNullException(nameof(outputHistory));
    }

    /// <summary>
    /// Unique identifier for this panel instance.
    /// </summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>
    /// Typed authoritative direct conversation provisioned at panel creation.
    /// Separate from <see cref="PanelId"/>.
    /// </summary>
    public ConversationId ConversationId { get; }

    /// <summary>
    /// Typed canonical actor identity for this panel.
    /// </summary>
    public ActorId ActorId => _actor.Id;

    /// <summary>
    /// Legacy projected agent identifier derived from the canonical actor row.
    /// </summary>
    public string AgentId => _actor.ProjectedLegacyId;

    /// <summary>
    /// Legacy projected display name derived from the canonical actor row.
    /// </summary>
    public string AgentName => _actor.DisplayName;

    /// <summary>
    /// Legacy projected avatar resource key derived from the canonical actor row.
    /// </summary>
    public string AvatarResourceKey => _actor.AvatarResourceKey;

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
    /// Whether this panel currently has an in-flight request.
    /// M3: Added for input-surface disable during in-flight requests.
    /// Set explicitly by the coordinator alongside Status transitions
    /// so the view can bind IsEnabled = !IsBusy without a converter.
    /// </summary>
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    /// <summary>
    /// Read-only projection of authoritative direct-conversation entries rendered
    /// with the existing Agent Panel string protocol.
    /// </summary>
    public ReadOnlyObservableCollection<string> OutputHistory { get; }

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

    /// <summary>
    /// Read-only disclosure status for the last admitted run in this panel.
    /// Phase 18 M4: Minimal visible indicator showing context disclosure state.
    /// </summary>
    private string _contextDisclosureStatus = string.Empty;
    public string ContextDisclosureStatus
    {
        get => _contextDisclosureStatus;
        set => this.RaiseAndSetIfChanged(ref _contextDisclosureStatus, value);
    }

    /// <summary>
    /// Human-readable resolved context policy for this panel's session.
    /// Phase 18 M5: Indicates effective policy and whether a session override is active.
    /// </summary>
    private string _contextPolicyStatusCaption = string.Empty;
    public string ContextPolicyStatusCaption
    {
        get => _contextPolicyStatusCaption;
        set => this.RaiseAndSetIfChanged(ref _contextPolicyStatusCaption, value);
    }

    /// <summary>
    /// True when this panel's session has an explicit policy override.
    /// </summary>
    private bool _isContextPolicyOverrideActive;
    public bool IsContextPolicyOverrideActive
    {
        get => _isContextPolicyOverrideActive;
        set => this.RaiseAndSetIfChanged(ref _isContextPolicyOverrideActive, value);
    }

    /// <summary>
    /// Selector index for the context policy combo:
    /// 0 = application default, 1 = Off, 2 = Minimal, 3 = Standard, 4 = Detailed.
    /// </summary>
    private int _contextPolicySelectorIndex;
    public int ContextPolicySelectorIndex
    {
        get => _contextPolicySelectorIndex;
        set => this.RaiseAndSetIfChanged(ref _contextPolicySelectorIndex, value);
    }
}
