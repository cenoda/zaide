using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Concrete host for agent panel collection and active-panel selection.
///
/// Owns the panel collection and active selection directly. Does not
/// reference UI, execution, Townhall, routing, or persistence concerns.
/// Optional <see cref="IConversationDraftState"/> keeps panel drafts aligned
/// with the conversation-owned draft contract (Phase 14 M7).
/// </summary>
public sealed class AgentPanelHost : IAgentPanelHost, INotifyPropertyChanged
{
    private readonly IActorCatalog _actorCatalog;
    private readonly IConversationStore _conversationStore;
    private readonly IConversationDraftState? _draftState;
    private readonly ObservableCollection<AgentPanelState> _panels;
    private readonly Dictionary<AgentPanelState, AgentPanelOutputHistoryProjection> _outputProjections = new();
    private readonly Dictionary<AgentPanelState, PropertyChangedEventHandler> _draftHandlers = new();
    private AgentPanelState? _activePanel;
    private int _nextSeedIndex;

    public ObservableCollection<AgentPanelState> Panels => _panels;

    public AgentPanelState? ActivePanel => _activePanel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentPanelHost(
        IActorCatalog actorCatalog,
        IConversationStore conversationStore,
        IConversationDraftState? draftState = null)
    {
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _draftState = draftState;
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

    /// <inheritdoc />
    public AgentPanelState CreatePanelForActor(ActorId actorId)
    {
        if (!_actorCatalog.TryGet(actorId, out var actor))
        {
            throw new ArgumentException(
                $"No catalog actor exists for id '{actorId.Value}'.",
                nameof(actorId));
        }

        return CreatePanelFromActor(actor);
    }

    /// <inheritdoc />
    public AgentPanelState GetOrCreatePanelForActor(ActorId actorId)
    {
        var existing = _panels.FirstOrDefault(p => p.ActorId == actorId);
        if (existing is not null)
        {
            return existing;
        }

        return CreatePanelForActor(actorId);
    }

    private AgentPanelState CreatePanelFromActor(Actor actor)
    {
        var conversation = _conversationStore.GetOrCreateDirectConversation(
            _actorCatalog.CanonicalHuman.Id,
            actor.Id);

        var outputProjection = new AgentPanelOutputHistoryProjection(
            _conversationStore,
            conversation.Id);

        var seededDraft = _draftState?.GetDraft(conversation.Id) ?? string.Empty;

        var panel = new AgentPanelState(actor, conversation.Id, outputProjection.Lines)
        {
            PanelId = Guid.NewGuid().ToString("N"),
            Status = "Idle",
            DraftInput = seededDraft
        };

        _outputProjections[panel] = outputProjection;
        AttachDraftSync(panel);

        _activePanel = panel;
        _panels.Add(panel);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActivePanel)));

        return panel;
    }

    private void AttachDraftSync(AgentPanelState panel)
    {
        if (_draftState is null)
        {
            return;
        }

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.DraftInput))
            {
                _draftState.SetDraft(panel.ConversationId, panel.DraftInput);
            }
        };
        panel.PropertyChanged += handler;
        _draftHandlers[panel] = handler;
    }

    private void DetachDraftSync(AgentPanelState panel)
    {
        if (_draftHandlers.Remove(panel, out var handler))
        {
            panel.PropertyChanged -= handler;
        }
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
    /// (Status, IsBusy, DraftInput). Disposes the panel output projection so
    /// closed panels no longer subscribe to conversation appends. If the closed
    /// panel was active, selects the neighbor at the same index (or the previous
    /// panel when the closed panel was last). Closing the final panel yields the
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

        // Flush draft into conversation-owned state before detaching the panel chrome.
        if (_draftState is not null)
        {
            _draftState.SetDraft(panel.ConversationId, panel.DraftInput);
        }

        DetachDraftSync(panel);
        DisposeOutputProjection(panel);
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

    private void DisposeOutputProjection(AgentPanelState panel)
    {
        if (_outputProjections.Remove(panel, out var projection))
        {
            projection.Dispose();
        }
    }
}
