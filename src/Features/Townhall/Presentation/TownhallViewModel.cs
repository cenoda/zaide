using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Infrastructure;
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
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentExecutionCoordinator _executionCoordinator;
    private readonly IAgentRouter? _agentRouter;
    private readonly TownhallConversationUiState _conversationUiState;
    private readonly IConversationWorkspacePersistenceBridge? _persistenceBridge;
    private readonly SerialDisposable _directBusySubscription = new();
    private string _draftText = string.Empty;
    private FilterMode _filterMode = FilterMode.All;
    private bool _isDirectSendBusy;

    /// <summary>
    /// Gets the list of channels.
    /// </summary>
    public ObservableCollection<Channel> Channels { get; }

    /// <summary>
    /// Gets the list of agents.
    /// </summary>
    public ObservableCollection<WorkspaceAgent> Agents { get; }

    /// <summary>
    /// Gets the list of direct conversation navigation rows.
    /// </summary>
    internal ObservableCollection<TownhallNavigationItem> DirectNavItems { get; } = new();

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
    /// Gets or sets the current draft text input. Syncs with TownhallState.DraftText on set
    /// and the per-conversation draft map (shared with Agent Panel thin host).
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
                if (_state.ActiveConversationId is { } activeConversationId)
                {
                    _conversationUiState.SetDraft(activeConversationId, value);
                    SyncPanelDraft(activeConversationId, value);
                    NotifyPresentationPersisted();
                }
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
    /// Gets or sets the authoritative active conversation selection.
    /// </summary>
    public ConversationId? ActiveConversationId
    {
        get => _state.ActiveConversationId;
        private set
        {
            if (_state.ActiveConversationId == value)
            {
                return;
            }

            _state.ActiveConversationId = value;
            this.RaisePropertyChanged(nameof(ActiveConversationId));
            UpdateDirectNavSelection();
            UpdateDirectSendBusyTracking();
        }
    }

    /// <summary>
    /// True when the active direct conversation has an in-flight agent request.
    /// Channel selection always yields false.
    /// </summary>
    public bool IsDirectSendBusy
    {
        get => _isDirectSendBusy;
        private set
        {
            if (_isDirectSendBusy == value)
            {
                return;
            }

            _isDirectSendBusy = value;
            this.RaisePropertyChanged(nameof(IsDirectSendBusy));
            this.RaisePropertyChanged(nameof(IsInputEnabled));
        }
    }

    /// <summary>
    /// Townhall input is enabled unless the active direct conversation is busy.
    /// </summary>
    public bool IsInputEnabled => !IsDirectSendBusy;

    /// <summary>
    /// Gets the ID of the currently active channel when a channel conversation is selected.
    /// </summary>
    public string? ActiveChannelId => _state.ActiveChannelId;

    /// <summary>
    /// Command to select a channel by its ID.
    /// Updates Channel.IsActive flags, active channel state, and message list.
    /// </summary>
    public ReactiveCommand<string, Unit> SelectChannelCommand { get; }

    /// <summary>
    /// Command to select a conversation by its authoritative id.
    /// </summary>
    public ReactiveCommand<ConversationId, Unit> SelectConversationCommand { get; }

    /// <summary>
    /// Command to open or select a direct conversation with the given agent actor.
    /// </summary>
    public ReactiveCommand<ActorId, Unit> OpenDirectConversationCommand { get; }

    /// <summary>
    /// Command to send the current draft message.
    /// Appends to the active channel or sends through the agent execution path for directs.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }

    /// <summary>
    /// Initializes a new instance of the TownhallViewModel class.
    /// </summary>
    public TownhallViewModel(
        TownhallState state,
        IActorCatalog actorCatalog,
        IConversationStore conversationStore,
        IAgentPanelHost panelHost,
        IAgentExecutionCoordinator executionCoordinator,
        IAgentRouter? agentRouter = null)
        : this(
            state,
            actorCatalog,
            conversationStore,
            panelHost,
            executionCoordinator,
            new TownhallConversationUiState(),
            persistenceBridge: null,
            persistenceService: null,
            agentRouter: agentRouter)
    {
    }

    internal TownhallViewModel(
        TownhallState state,
        IActorCatalog actorCatalog,
        IConversationStore conversationStore,
        IAgentPanelHost panelHost,
        IAgentExecutionCoordinator executionCoordinator,
        TownhallConversationUiState conversationUiState,
        IConversationWorkspacePersistenceBridge? persistenceBridge,
        ConversationPersistenceService? persistenceService,
        IAgentRouter? agentRouter = null)
    {
        _ = persistenceService;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _executionCoordinator = executionCoordinator ?? throw new ArgumentNullException(nameof(executionCoordinator));
        _agentRouter = agentRouter;
        _conversationUiState = conversationUiState ?? throw new ArgumentNullException(nameof(conversationUiState));
        _persistenceBridge = persistenceBridge;

        _conversationStore.EntryAppended += OnConversationEntryAppended;

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
            SelectConversation(ConversationId.ForChannel(channelId));
        });

        SelectConversationCommand = ReactiveCommand.Create<ConversationId>(
            id => SelectConversation(id));

        OpenDirectConversationCommand = ReactiveCommand.Create<ActorId>(OpenDirectConversation);

        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessageAsync);
        UpdateDirectSendBusyTracking();
    }

    private async Task SendMessageAsync()
    {
        var draft = DraftText?.Trim();
        if (string.IsNullOrEmpty(draft))
        {
            return;
        }

        if (_state.ActiveChannelId is not null)
        {
            LogActivity(
                entryKind: ConversationEntryKind.UserChat,
                content: draft,
                author: _actorCatalog.CanonicalHuman.Id,
                senderId: _actorCatalog.CanonicalHuman.ProjectedLegacyId,
                senderName: _actorCatalog.CanonicalHuman.DisplayName);
            ClearActiveConversationDraft();
            return;
        }

        if (_state.ActiveConversationId is not { } activeConversationId)
        {
            return;
        }

        if (!_conversationStore.TryGet(activeConversationId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            return;
        }

        var panel = EnsurePanelForDirectConversation(conversation);
        UpdateDirectSendBusyTracking();

        if (_executionCoordinator.IsConversationBusy(conversation.Id) || panel.IsBusy)
        {
            return;
        }

        // Prefer router so @mention targets resolve via catalog ActorId roster
        // without requiring an open target panel tab.
        if (_agentRouter is not null)
        {
            var routeResult = await _agentRouter.RouteAndExecuteAsync(panel.PanelId, draft);
            if (routeResult.Success
                || routeResult.ExecutionResult is not null
                || !string.IsNullOrEmpty(routeResult.FailureReason))
            {
                // Admitted execution or recorded routing failure both clear the input.
                // Empty/no-op rejects leave the draft for re-send.
                if (routeResult.Success || routeResult.ExecutionResult is not null)
                {
                    ClearActiveConversationDraft();
                }
            }

            return;
        }

        var result = await _executionCoordinator.SendAsync(panel.PanelId, draft);
        if (result is not null)
        {
            ClearActiveConversationDraft();
        }
    }

    private void SelectConversation(ConversationId conversationId, bool markRead = true)
    {
        if (!_conversationStore.TryGet(conversationId, out var conversation))
        {
            return;
        }

        var previousId = _state.ActiveConversationId;
        if (previousId is { } previous && previous != conversationId)
        {
            _conversationUiState.SetDraft(previous, DraftText);
            NotifyPresentationPersisted();
        }

        ActiveConversationId = conversationId;

        if (previousId != conversationId)
        {
            DraftText = _conversationUiState.GetDraft(conversationId);
            NotifyPresentationPersisted();
        }

        if (conversation.Kind == ConversationKind.Channel
            && conversationId.TryGetChannelId(out var channelId))
        {
            ApplyChannelSelection(channelId);
            if (markRead)
            {
                MarkConversationRead(conversationId);
            }
            else
            {
                ApplyUnreadPresentation(conversation);
            }

            return;
        }

        if (conversation.Kind == ConversationKind.Direct)
        {
            ApplyDirectSelection(conversation);
            if (markRead)
            {
                MarkConversationRead(conversationId);
            }
            else
            {
                ApplyUnreadPresentation(conversation);
            }
        }
    }

    private void ClearActiveConversationDraft()
    {
        if (_state.ActiveConversationId is { } activeConversationId)
        {
            _conversationUiState.ClearDraft(activeConversationId);
        }

        DraftText = string.Empty;
    }

    private void MarkConversationRead(ConversationId conversationId)
    {
        if (!_conversationStore.TryGet(conversationId, out var conversation))
        {
            return;
        }

        if (conversation.Entries.Count == 0)
        {
            _conversationUiState.SetLastReadEntryId(conversationId, null);
        }
        else
        {
            _conversationUiState.SetLastReadEntryId(
                conversationId,
                conversation.Entries[^1].Id);
        }

        ApplyUnreadPresentation(conversation);
        NotifyPresentationPersisted();
    }

    private void AdvanceLastRead(ConversationId conversationId, ConversationEntry entry)
    {
        _conversationUiState.SetLastReadEntryId(conversationId, entry.Id);
        if (_conversationStore.TryGet(conversationId, out var conversation))
        {
            ApplyUnreadPresentation(conversation);
        }
    }

    private void ApplyUnreadPresentation(Conversation conversation)
    {
        var isUnread = _conversationUiState.IsUnread(conversation);

        if (conversation.Kind == ConversationKind.Channel
            && conversation.Id.TryGetChannelId(out var channelId))
        {
            var channel = _state.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is not null)
            {
                channel.HasUnread = isUnread;
            }

            return;
        }

        if (conversation.Kind == ConversationKind.Direct)
        {
            var item = DirectNavItems.FirstOrDefault(i => i.ConversationId == conversation.Id);
            if (item is not null)
            {
                item.HasUnread = isUnread;
            }
        }
    }

    private void OpenDirectConversation(ActorId agentActorId)
    {
        if (agentActorId == _actorCatalog.CanonicalHuman.Id)
        {
            return;
        }

        var conversation = _conversationStore.GetOrCreateDirectConversation(
            _actorCatalog.CanonicalHuman.Id,
            agentActorId);
        RefreshDirectNavItems();
        SelectConversation(conversation.Id);
    }

    private void ApplyChannelSelection(string channelId)
    {
        var oldActiveId = _state.ActiveChannelId;
        if (oldActiveId == channelId)
        {
            return;
        }

        foreach (var c in _state.Channels)
        {
            c.IsActive = c.Id == channelId;
        }

        _state.ActiveChannelId = channelId;
        this.RaisePropertyChanged(nameof(ActiveChannelId));

        if (!string.IsNullOrEmpty(channelId) && !string.IsNullOrEmpty(oldActiveId))
        {
            var channel = _state.Channels.FirstOrDefault(c => c.Id == channelId);
            var channelName = channel?.Name ?? channelId;
            LogActivity(
                entryKind: ConversationEntryKind.ChannelEvent,
                content: $"Switched to #{channelName}",
                author: _actorCatalog.CanonicalHuman.Id,
                senderId: _actorCatalog.CanonicalHuman.ProjectedLegacyId,
                senderName: _actorCatalog.CanonicalHuman.DisplayName);
        }

        if (_state.ChannelMessages.TryGetValue(channelId, out var channelMsgs))
        {
            Messages = channelMsgs;
        }
        else
        {
            Messages = new ObservableCollection<TownhallMessage>();
        }
    }

    private void ApplyDirectSelection(Conversation conversation)
    {
        foreach (var c in _state.Channels)
        {
            c.IsActive = false;
        }

        if (_state.ActiveChannelId is not null)
        {
            _state.ActiveChannelId = null;
            this.RaisePropertyChanged(nameof(ActiveChannelId));
        }

        Messages = ProjectDirectMessages(conversation);
        UpdateDirectSendBusyTracking();
    }

    private AgentPanelState? FindPanelForConversation(ConversationId conversationId) =>
        _panelHost.Panels.FirstOrDefault(panel => panel.ConversationId == conversationId);

    private AgentPanelState EnsurePanelForDirectConversation(Conversation conversation)
    {
        var peerActorId = ResolveDirectPeerActorId(conversation);
        return _panelHost.GetOrCreatePanelForActor(peerActorId);
    }

    private void SyncPanelDraft(ConversationId conversationId, string draft)
    {
        var panel = FindPanelForConversation(conversationId);
        if (panel is not null && panel.DraftInput != draft)
        {
            panel.DraftInput = draft;
        }
    }

    private ActorId ResolveDirectPeerActorId(Conversation conversation)
    {
        var humanId = _actorCatalog.CanonicalHuman.Id;
        var peer = conversation.Participants.All.FirstOrDefault(participant => participant != humanId);
        if (peer == default)
        {
            throw new InvalidOperationException(
                $"Direct conversation '{conversation.Id.Value}' has no non-human participant.");
        }

        return peer;
    }

    private void UpdateDirectSendBusyTracking()
    {
        _directBusySubscription.Disposable = null;

        if (_state.ActiveChannelId is not null
            || _state.ActiveConversationId is not { } activeConversationId
            || !_conversationStore.TryGet(activeConversationId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            IsDirectSendBusy = false;
            return;
        }

        // Conversation-keyed busy survives panel close and navigation (M7).
        IsDirectSendBusy = _executionCoordinator.IsConversationBusy(activeConversationId);

        Action<ConversationId, bool> busyHandler = (conversationId, isBusy) =>
        {
            if (conversationId == activeConversationId)
            {
                IsDirectSendBusy = isBusy;
            }
        };
        _executionCoordinator.ConversationBusyChanged += busyHandler;

        // Also project open-panel IsBusy while a thin host exists.
        var panel = FindPanelForConversation(activeConversationId);
        PropertyChangedEventHandler? panelHandler = null;
        if (panel is not null)
        {
            if (panel.IsBusy)
            {
                IsDirectSendBusy = true;
            }

            panelHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(AgentPanelState.IsBusy))
                {
                    IsDirectSendBusy = panel.IsBusy
                        || _executionCoordinator.IsConversationBusy(activeConversationId);
                }
            };
            panel.PropertyChanged += panelHandler;
        }

        _directBusySubscription.Disposable = Disposable.Create(() =>
        {
            _executionCoordinator.ConversationBusyChanged -= busyHandler;
            if (panel is not null && panelHandler is not null)
            {
                panel.PropertyChanged -= panelHandler;
            }
        });
    }

    private ObservableCollection<TownhallMessage> ProjectDirectMessages(Conversation conversation)
    {
        var projected = new ObservableCollection<TownhallMessage>();
        foreach (var entry in conversation.Entries)
        {
            projected.Add(TownhallEntryProjection.ToTownhallMessage(entry, _actorCatalog));
        }

        return projected;
    }

    private void RefreshDirectNavItems()
    {
        var humanId = _actorCatalog.CanonicalHuman.Id;
        var selectedId = _state.ActiveConversationId;
        var items = _conversationStore.ListConversations()
            .Where(c => c.Kind == ConversationKind.Direct && c.Participants.Contains(humanId))
            .Select(c => CreateDirectNavItem(c, humanId, selectedId))
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        DirectNavItems.Clear();
        foreach (var item in items)
        {
            DirectNavItems.Add(item);
        }
    }

    private TownhallNavigationItem CreateDirectNavItem(
        Conversation conversation,
        ActorId humanId,
        ConversationId? selectedId)
    {
        var peer = conversation.Participants.All.FirstOrDefault(participant => participant != humanId);
        string label;
        ActorId? peerId = null;
        if (peer != default)
        {
            peerId = peer;
            label = _actorCatalog.TryGet(peer, out var actor) && !string.IsNullOrWhiteSpace(actor.DisplayName)
                ? actor.DisplayName
                : peer.Value;
        }
        else
        {
            label = "Direct";
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            label = "Direct";
        }

        return new TownhallNavigationItem
        {
            ConversationId = conversation.Id,
            Kind = TownhallNavigationKind.Direct,
            Label = label,
            PeerActorId = peerId,
            IsSelected = selectedId.HasValue && selectedId.Value == conversation.Id,
            HasUnread = _conversationUiState.IsUnread(conversation)
        };
    }

    private void UpdateDirectNavSelection()
    {
        var selectedId = _state.ActiveConversationId;
        foreach (var item in DirectNavItems)
        {
            item.IsSelected = selectedId.HasValue && item.ConversationId == selectedId.Value;
            if (_conversationStore.TryGet(item.ConversationId, out var conversation))
            {
                item.HasUnread = _conversationUiState.IsUnread(conversation);
            }
        }
    }

    private void OnConversationEntryAppended(ConversationId conversationId, ConversationEntry entry)
    {
        // Must never throw into ConversationStore.AppendEntry callers (agent send
        // path). A UI rebind NRE here was previously recorded as the assistant
        // ExecutionFailure: "Object reference not set to an instance of an object."
        try
        {
            if (!_conversationStore.TryGet(conversationId, out var conversation))
            {
                return;
            }

            var isActive = _state.ActiveConversationId == conversationId;
            if (isActive)
            {
                // Active + visible: advance last-read so appends do not leave sticky unread.
                AdvanceLastRead(conversationId, entry);
            }
            else
            {
                // Inactive: leave cursor; derived unread becomes true when history advanced.
                ApplyUnreadPresentation(conversation);
            }

            if (conversation.Kind == ConversationKind.Direct)
            {
                RefreshDirectNavItems();

                if (isActive)
                {
                    Messages.Add(TownhallEntryProjection.ToTownhallMessage(entry, _actorCatalog));
                }
            }
        }
        catch
        {
            // Swallow: presentation projection must not fail the write path.
        }
    }

    /// <summary>
    /// Appends a classified activity entry to the active channel's message collection.
    /// Writes the authoritative typed entry to the channel conversation, then
    /// projects it into the legacy Townhall compatibility collection.
    /// </summary>
    private void LogActivity(
        ConversationEntryKind entryKind,
        string content,
        ActorId author,
        string senderId,
        string senderName)
    {
        if (_state.ActiveChannelId is null)
            return;

        if (!_conversationStore.TryGetChannelConversation(_state.ActiveChannelId, out var conversation))
            return;

        AppendMirroredActivity(conversation.Id, entryKind, content, author, senderId, senderName);
    }

    private void AppendMirroredActivity(
        ConversationId conversationId,
        ConversationEntryKind entryKind,
        string content,
        ActorId author,
        string senderId,
        string senderName)
    {
        if (!_conversationStore.TryGet(conversationId, out var conversation))
            return;

        if (conversation.Kind != ConversationKind.Channel)
            return;

        if (!conversationId.TryGetChannelId(out var channelId))
            return;

        if (!_state.ChannelMessages.ContainsKey(channelId))
        {
            _state.ChannelMessages[channelId] = new ObservableCollection<TownhallMessage>();
        }

        var messagesList = _state.ChannelMessages[channelId];
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
        if (_persistenceBridge?.WasRestoredFromPersistence == true)
        {
            InitializeFromPersistedSession();
            return;
        }

        InitializeSeededSession();
    }

    private void InitializeFromPersistedSession()
    {
        foreach (var channel in _state.Channels)
        {
            _conversationStore.CreateChannelConversation(channel.Id);
            RebuildChannelMessages(channel.Id);
        }

        SeedWorkspaceAgents();
        RefreshDirectNavItems();

        foreach (var conversation in _conversationStore.ListConversations())
        {
            ApplyUnreadPresentation(conversation);
        }

        if (_persistenceBridge?.RestoredActiveConversationId is { } activeValue
            && TryParseConversationId(activeValue, out var activeConversationId)
            && _conversationStore.TryGet(activeConversationId, out _))
        {
            SelectConversation(activeConversationId, markRead: false);
            DraftText = _conversationUiState.GetDraft(activeConversationId);
            return;
        }

        if (_state.Channels.Count > 0)
        {
            SelectConversation(ConversationId.ForChannel(_state.Channels[0].Id), markRead: false);
            DraftText = string.Empty;
        }
    }

    private void InitializeSeededSession()
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
        SelectConversation(ConversationId.ForChannel(townhallMain.Id));
        RefreshDirectNavItems();

        // Create initial agents from the canonical actor catalog.
        SeedWorkspaceAgents();
        DraftText = string.Empty;
    }

    private void SeedWorkspaceAgents()
    {
        if (_state.Agents.Count > 0)
        {
            return;
        }

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
    }

    private void RebuildChannelMessages(string channelId)
    {
        if (!_conversationStore.TryGetChannelConversation(channelId, out var conversation))
        {
            _state.ChannelMessages[channelId] = new ObservableCollection<TownhallMessage>();
            return;
        }

        var messages = new ObservableCollection<TownhallMessage>();
        foreach (var entry in conversation.Entries)
        {
            messages.Add(TownhallEntryProjection.ToTownhallMessage(entry, _actorCatalog));
        }

        _state.ChannelMessages[channelId] = messages;
    }

    private static bool TryParseConversationId(string value, out ConversationId conversationId)
    {
        try
        {
            conversationId = ConversationId.FromValue(value);
            return true;
        }
        catch
        {
            conversationId = default;
            return false;
        }
    }

    private void NotifyPresentationPersisted() =>
        _persistenceBridge?.NotifyPresentationStateChanged();

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
