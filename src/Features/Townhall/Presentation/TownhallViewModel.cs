using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// ViewModel for the Townhall workspace.
/// Exposes channels, messages, agents, and draft state as reactive properties.
/// Commands: select channel, send message.
/// Initializes explicit in-memory session seed state for first run.
/// Messages are stored per-channel in TownhallState.ChannelMessages.
/// </summary>
public class TownhallViewModel : ReactiveObject
{
    private readonly TownhallState _state;
    private readonly IActorCatalog _actorCatalog;
    private readonly IConversationStore _conversationStore;
    private string _draftText = string.Empty;
    private FilterMode _filterMode = FilterMode.All;

    /// <summary>
    /// Gets the list of channels.
    /// </summary>
    public ObservableCollection<Channel> Channels { get; }

    /// <summary>
    /// Gets the list of agents.
    /// </summary>
    public ObservableCollection<WorkspaceAgent> Agents { get; }

    private ObservableCollection<TownhallMessage> _messages = new();

    /// <summary>
    /// Gets the list of messages for the active channel.
    /// Updates whenever ActiveChannelId changes to reflect the current channel's messages.
    /// Raises PropertyChanged when the collection reference changes.
    /// </summary>
    public ObservableCollection<TownhallMessage> Messages
    {
        get => _messages;
        private set
        {
            if (_messages != value)
            {
                _messages = value;
                this.RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the current draft text input. Syncs with TownhallState.DraftText on set.
    /// </summary>
    public string DraftText
    {
        get => _draftText;
        set
        {
            if (_draftText != value)
            {
                _draftText = value;
                this.RaisePropertyChanged();
                // Sync to state for M3 integration
                _state.DraftText = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the current filter mode for the chat panel (All / ChatOnly / ActivityOnly).
    /// Default All. Raises PropertyChanged on change.
    /// </summary>
    public FilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (_filterMode != value)
            {
                _filterMode = value;
                this.RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Computed filtered view of Messages based on current FilterMode.
    /// Reacts to changes in FilterMode or Messages collection (via WhenAnyValue + Select).
    /// </summary>
    public IObservable<System.Collections.Generic.IReadOnlyList<TownhallMessage>> FilteredMessages { get; }

    /// <summary>
    /// Gets or sets the ID of the currently active channel.
    /// Also updates Channel.IsActive flags for all channels and syncs Messages collection.
    /// </summary>
    public string? ActiveChannelId
    {
        get => _state.ActiveChannelId;
        private set
        {
            var oldActiveId = _state.ActiveChannelId;
            if (oldActiveId == value) return;

            // Update all channel active states
            foreach (var c in _state.Channels)
            {
                c.IsActive = c.Id == value;
            }

            _state.ActiveChannelId = value;
            this.RaisePropertyChanged(nameof(ActiveChannelId));

            // Log channel switch event to the *newly active* channel (only for actual switches, not initial activation)
            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(oldActiveId))
            {
                var channel = _state.Channels.FirstOrDefault(c => c.Id == value);
                var channelName = channel?.Name ?? value;
                LogActivity(
                    kind: TownhallMessageKind.ChannelEvent,
                    content: $"Switched to #{channelName}",
                    author: _actorCatalog.CanonicalHuman.Id,
                    senderId: _actorCatalog.CanonicalHuman.ProjectedLegacyId,
                    senderName: _actorCatalog.CanonicalHuman.DisplayName);
            }

            // Update the Messages collection to reflect current channel's messages
            if (!string.IsNullOrEmpty(value) && _state.ChannelMessages.TryGetValue(value, out var channelMsgs))
            {
                this.Messages = channelMsgs;
            }
            else
            {
                // Fallback: use empty collection
                this.Messages = new ObservableCollection<TownhallMessage>();
            }
        }
    }

    /// <summary>
    /// Command to select a channel by its ID.
    /// Updates Channel.IsActive flags, active channel state, and message list.
    /// </summary>
    public ReactiveCommand<string, Unit> SelectChannelCommand { get; }

    /// <summary>
    /// Command to send the current draft message.
    /// Appends to the active channel's message list.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }

    /// <summary>
    /// Initializes a new instance of the TownhallViewModel class.
    /// </summary>
    public TownhallViewModel(
        TownhallState state,
        IActorCatalog actorCatalog,
        IConversationStore conversationStore)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));

        // Initialize explicit session seed state
        InitializeSessionState();

        // Setup reactive properties based on state
        Channels = _state.Channels;
        Agents = _state.Agents;

        // Reactive filtered messages: recomputes on FilterMode or Messages (ref or collection content).
        // Uses raw PropertyChanged event from INotifyPropertyChanged rather than WhenAnyValue,
        // because all WhenAnyValue overloads in this ReactiveUI version trigger
        // RxAppBuilder.EnsureInitialized() via ObservableForProperty, which fails in isolated
        // unit-test hosts that don't have a full ReactiveUI app bootstrap.
        //
        // A single top-level Switch() ensures only one CollectionChanged subscription is ever live
        // at a time — when Messages changes (e.g., channel switch), the previous collection's
        // subscription is torn down before subscribing to the new one, avoiding an unbounded leak.
        var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => PropertyChanged += h,
                h => PropertyChanged -= h)
            .Select(e => e.EventArgs.PropertyName);
        var filterModeChanged = propertyChanged
            .Where(name => name == nameof(FilterMode))
            .Select(_ => Unit.Default);
        // Seed with the current Messages collection (evaluated lazily at
        // subscription time via Defer) so its CollectionChanged is subscribed
        // immediately. InitializeSessionState() sets Messages to the active
        // channel's collection before this observable exists, so without this
        // seed the initial collection never gets a live CollectionChanged
        // subscription — mirrored activity (e.g. agent-panel sends) would not
        // refresh the chat panel until a channel switch or filter change.
        // Defer (not a plain eager seed) is required so the seed reflects the
        // *current* Messages at subscribe time, not the value captured when the
        // observable chain was constructed.
        var messagesSeed = Observable.Defer(() => Observable.Return(Messages ?? new ObservableCollection<TownhallMessage>()));
        var messagesRefChanged = propertyChanged
            .Where(name => name == nameof(Messages))
            .Select(_ => Messages ?? new ObservableCollection<TownhallMessage>());
        var messagesContentChanged = Observable.Merge(messagesSeed, messagesRefChanged)
            .DistinctUntilChanged()
            .Select(m => Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => m.CollectionChanged += h,
                    h => m.CollectionChanged -= h)
                .Select(_ => Unit.Default)
                .StartWith(Unit.Default))
            .Switch();
        FilteredMessages = Observable.Merge(filterModeChanged, messagesContentChanged)
            .StartWith(Unit.Default)
            .Select(_ => (System.Collections.Generic.IReadOnlyList<TownhallMessage>)ApplyFilter());

        // Selected channel command - updates channel active flags and active channel id
        SelectChannelCommand = ReactiveCommand.Create<string>(channelId =>
        {
            ActiveChannelId = channelId;
        });

        // Send message command - validates and appends to active channel's message list
        SendMessageCommand = ReactiveCommand.Create(() =>
        {
            var draft = DraftText?.Trim();
            if (string.IsNullOrEmpty(draft))
                return;

            if (_state.ActiveChannelId is null)
                return;

            LogActivity(
                kind: TownhallMessageKind.Chat,
                content: draft,
                author: _actorCatalog.CanonicalHuman.Id,
                senderId: _actorCatalog.CanonicalHuman.ProjectedLegacyId,
                senderName: _actorCatalog.CanonicalHuman.DisplayName);

            // Clear draft after sending
            DraftText = string.Empty;
        });
    }

    /// <summary>
    /// Appends a mirrored activity entry to the active channel's message collection.
    /// This is the narrow public surface for app-layer mirroring (e.g., agent-panel
    /// interactions mirrored into Townhall). Keeps channel/message-list invariants
    /// internal to this ViewModel.
    /// </summary>
    /// <param name="kind">The kind of entry (Chat, AgentError, etc.).</param>
    /// <param name="content">The text content of the entry.</param>
    /// <param name="author">Authoritative typed actor identity for the entry.</param>
    /// <param name="senderId">Legacy projected sender ID for Townhall compatibility.</param>
    /// <param name="senderName">Legacy projected sender name for Townhall compatibility.</param>
    public void AddMirroredActivity(
        TownhallMessageKind kind,
        string content,
        ActorId author,
        string senderId,
        string senderName)
    {
        LogActivity(kind, content, author, senderId, senderName);
    }

    /// <summary>
    /// Appends a classified activity entry to the active channel's message collection.
    /// Writes the authoritative typed entry to the channel conversation, then
    /// projects it into the legacy Townhall compatibility collection.
    /// </summary>
    private void LogActivity(
        TownhallMessageKind kind,
        string content,
        ActorId author,
        string senderId,
        string senderName)
    {
        if (_state.ActiveChannelId is null)
            return;

        if (!_conversationStore.TryGetChannelConversation(_state.ActiveChannelId, out var conversation))
            return;

        // Ensure the channel has a messages list in the dictionary
        if (!_state.ChannelMessages.ContainsKey(_state.ActiveChannelId))
        {
            _state.ChannelMessages[_state.ActiveChannelId] = new ObservableCollection<TownhallMessage>();
        }

        var messagesList = _state.ChannelMessages[_state.ActiveChannelId];
        var entryKind = TownhallEntryProjection.ClassifyTownhallMirror(
            kind,
            author,
            content,
            _actorCatalog);
        var timestamp = DateTimeOffset.UtcNow;
        var typedEntry = TownhallEntryProjection.CreateTypedEntry(
            entryKind,
            author,
            timestamp,
            content);

        _conversationStore.AppendEntry(conversation.Id, typedEntry);

        var entry = TownhallEntryProjection.ToTownhallMessage(
            typedEntry,
            _actorCatalog,
            senderId,
            senderName);

        messagesList.Add(entry);
    }

    /// <summary>
    /// Initializes explicit initial session state for first run using in-memory seed data.
    /// Seeds channels, agents, and starter messages required for a usable Townhall workspace.
    /// </summary>
    private void InitializeSessionState()
    {
        // Create initial channels
        var townhallMain = new Channel { Id = "channel-1", Name = "townhall-main", IsPinned = true };
        var aiStatus = new Channel { Id = "channel-2", Name = "ai-status", IsPinned = false };
        var codebaseRefactoring = new Channel { Id = "channel-3", Name = "codebase-refactor", IsPinned = true };

        _state.Channels.Add(townhallMain);
        _state.Channels.Add(aiStatus);
        _state.Channels.Add(codebaseRefactoring);

        foreach (var channel in _state.Channels)
        {
            _conversationStore.CreateChannelConversation(channel.Id);
        }

        // Create empty per-channel message collections in state
        _state.ChannelMessages[townhallMain.Id] = new ObservableCollection<TownhallMessage>();
        _state.ChannelMessages[aiStatus.Id] = new ObservableCollection<TownhallMessage>();
        _state.ChannelMessages[codebaseRefactoring.Id] = new ObservableCollection<TownhallMessage>();

        // Set initial active channel (which also sets IsActive flags and Messages collection)
        ActiveChannelId = townhallMain.Id;

        // Create initial agents from the canonical actor catalog.
        var user = _actorCatalog.CanonicalHuman;
        var agent1 = _actorCatalog.CanonicalTownhallAgent;
        _state.Agents.Add(new WorkspaceAgent(user)
        {
            Role = "user",
            Status = AgentStatus.Active,
            HasWarning = false
        });
        _state.Agents.Add(new WorkspaceAgent(agent1)
        {
            Role = "agent",
            Status = AgentStatus.Active,
            HasWarning = false
        });

        // Set initial draft text (syncs with state automatically via setter)
        DraftText = string.Empty;
    }

    private System.Collections.ObjectModel.ReadOnlyCollection<TownhallMessage> ApplyFilter()
    {
        var source = Messages ?? new ObservableCollection<TownhallMessage>();
        return FilterMode switch
        {
            FilterMode.ChatOnly => new System.Collections.ObjectModel.ReadOnlyCollection<TownhallMessage>(
                source.Where(m => m.Kind == TownhallMessageKind.Chat).ToList()),
            FilterMode.ActivityOnly => new System.Collections.ObjectModel.ReadOnlyCollection<TownhallMessage>(
                source.Where(m => m.Kind != TownhallMessageKind.Chat).ToList()),
            _ => new System.Collections.ObjectModel.ReadOnlyCollection<TownhallMessage>(source.ToList())
        };
    }
}
