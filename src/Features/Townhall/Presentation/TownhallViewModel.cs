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
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
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
public class TownhallViewModel : ReactiveObject, IDisposable
{
    private readonly TownhallState _state;
    private readonly IActorCatalog _actorCatalog;
    private readonly IConversationStore _conversationStore;
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentExecutionCoordinator _executionCoordinator;
    private readonly IAgentContextSessionPolicyService _sessionPolicyService;
    private readonly IAgentActorBackendSelectionService? _backendSelectionService;
    private readonly AgentBackendBindingPresenter? _backendBindingPresenter;
    private readonly IAgentRouter? _agentRouter;
    private readonly IAgentSessionService? _sessionService;
    private readonly TownhallConversationUiState _conversationUiState;
    private readonly IConversationWorkspacePersistenceBridge? _persistenceBridge;
    private readonly AgentTransparencyManagementViewModel? _transparencyManagement;
    private readonly SerialDisposable _directBusySubscription = new();
    private readonly IDisposable? _sessionEventsSubscription;
    /// <summary>
    /// Guards <see cref="DirectNavItems"/> mutations and enumeration. Entry-appended
    /// projection may run off the UI thread while navigation commands refresh the
    /// collection; <see cref="ObservableCollection{T}"/> is not thread-safe.
    /// </summary>
    private readonly object _directNavSync = new();
    private bool _disposed;
    private string _draftText = string.Empty;
    private FilterMode _filterMode = FilterMode.All;
    private bool _isDirectSendBusy;
    private bool _isContextPolicySelectorVisible;
    private string _contextPolicyStatusCaption = string.Empty;
    private bool _isContextPolicyOverrideActive;
    private int _contextPolicySelectorIndex;
    private bool _isBackendBindingStatusVisible;
    private string _backendBindingLabel = string.Empty;
    private string _backendAuthStatusCaption = string.Empty;
    private bool _isBackendDisconnected;
    private string _backendCapabilityCaption = string.Empty;
    private string _backendSettingsCaption = string.Empty;
    private string _backendMutationErrorCaption = string.Empty;
    private bool _canBindNativeHarness;
    private bool _canUnbindBackend;
    private bool _canEndSession;
    private string _acpRuntimeCaption = string.Empty;
    private bool _canProbeAcp;
    private bool _canAuthenticateAcp;
    private bool _canLogoutAcp;
    private bool _canBindAcp;
    private bool _showAcpConfig;
    private string _acpExecutableDraft = string.Empty;
    private string _acpArgumentsDraft = string.Empty;
    private string _acpExpectedNameDraft = string.Empty;
    private string _acpExpectedVersionDraft = string.Empty;
    private string _acpAuthMethodDraft = string.Empty;
    private ActorId? _activeBackendActorId;

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

    /// <summary>
    /// Gets the agent panel host panels for Phase 18 M4 context disclosure status binding.
    /// </summary>
    internal ObservableCollection<AgentPanelState> AgentPanels => _panelHost.Panels;

    internal AgentTransparencyManagementViewModel? TransparencyManagement =>
        _transparencyManagement;

    /// <summary>
    /// Publishes the active Townhall direct-conversation context into the memory
    /// lifecycle surface. Switching conversation clears selection and reloads
    /// when the panel is open.
    /// </summary>
    internal void PublishMemoryTownhallContext()
    {
        if (_transparencyManagement is null)
        {
            return;
        }

        ConversationId? conversationId = _state.ActiveConversationId;
        ActorId? agentActorId = null;
        string? sessionId = null;

        if (conversationId is { } activeId
            && _conversationStore.TryGet(activeId, out var conversation)
            && conversation.Kind == ConversationKind.Direct)
        {
            var peer = conversation.Participants.All.FirstOrDefault(p => p != _actorCatalog.CanonicalHuman.Id);
            if (peer != default)
            {
                agentActorId = peer;
            }

            var snapshot = _sessionService?.TryGetSessionSnapshot(activeId);
            if (snapshot is not null && snapshot.Status != AgentSessionStatus.Ended)
            {
                sessionId = snapshot.SessionId.Value;
            }
        }

        // Project/Shared identity is derived from the opened workspace inside
        // the memory inspection owner; Townhall does not invent a project id.
        _ = _transparencyManagement.BindMemoryTownhallContextAsync(
            new AgentMemoryInspectionViewModel.TownhallContext(
                conversationId,
                agentActorId,
                sessionId,
                projectId: null));
    }

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
    /// and the per-conversation draft map (conversation-owned; Phase 14 M8).
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
    /// Header label for the active conversation surface (#channel-name or agent display name).
    /// Derived from <see cref="ActiveConversationId"/> — not <see cref="ActiveChannelId"/>.
    /// </summary>
    public string ActiveConversationHeaderLabel { get; private set; } = string.Empty;

    /// <summary>
    /// Input placeholder for the active conversation. Derived from <see cref="ActiveConversationId"/>.
    /// </summary>
    public string ActiveConversationInputPlaceholder { get; private set; } = "Message...";

    /// <summary>
    /// True when the active conversation is a direct agent session that exposes
    /// the context policy selector.
    /// </summary>
    public bool IsContextPolicySelectorVisible
    {
        get => _isContextPolicySelectorVisible;
        private set => this.RaiseAndSetIfChanged(ref _isContextPolicySelectorVisible, value);
    }

    /// <summary>
    /// Resolved context policy caption for the active direct conversation.
    /// </summary>
    public string ContextPolicyStatusCaption
    {
        get => _contextPolicyStatusCaption;
        private set => this.RaiseAndSetIfChanged(ref _contextPolicyStatusCaption, value);
    }

    /// <summary>
    /// True when the active direct conversation has a session policy override.
    /// </summary>
    public bool IsContextPolicyOverrideActive
    {
        get => _isContextPolicyOverrideActive;
        private set => this.RaiseAndSetIfChanged(ref _isContextPolicyOverrideActive, value);
    }

    /// <summary>
    /// Combo selector index for the active direct conversation policy.
    /// </summary>
    public int ContextPolicySelectorIndex
    {
        get => _contextPolicySelectorIndex;
        private set => this.RaiseAndSetIfChanged(ref _contextPolicySelectorIndex, value);
    }

    /// <summary>
    /// True when the active direct conversation exposes backend binding status.
    /// </summary>
    public bool IsBackendBindingStatusVisible
    {
        get => _isBackendBindingStatusVisible;
        private set => this.RaiseAndSetIfChanged(ref _isBackendBindingStatusVisible, value);
    }

    /// <summary>
    /// Backend label for the active direct conversation binding.
    /// </summary>
    public string BackendBindingLabel
    {
        get => _backendBindingLabel;
        private set => this.RaiseAndSetIfChanged(ref _backendBindingLabel, value);
    }

    /// <summary>
    /// Authentication status caption for the active direct conversation binding.
    /// </summary>
    public string BackendAuthStatusCaption
    {
        get => _backendAuthStatusCaption;
        private set => this.RaiseAndSetIfChanged(ref _backendAuthStatusCaption, value);
    }

    /// <summary>
    /// True when the active direct conversation backend is disconnected or failed.
    /// </summary>
    public bool IsBackendDisconnected
    {
        get => _isBackendDisconnected;
        private set => this.RaiseAndSetIfChanged(ref _isBackendDisconnected, value);
    }

    /// <summary>
    /// Capability truth caption (configured / available / usable distinctions).
    /// </summary>
    public string BackendCapabilityCaption
    {
        get => _backendCapabilityCaption;
        private set => this.RaiseAndSetIfChanged(ref _backendCapabilityCaption, value);
    }

    /// <summary>
    /// Settings / secret ownership caption for the active binding.
    /// </summary>
    public string BackendSettingsCaption
    {
        get => _backendSettingsCaption;
        private set => this.RaiseAndSetIfChanged(ref _backendSettingsCaption, value);
    }

    /// <summary>
    /// Actionable mutation error (Busy / Conflict / PersistenceFailed / validation).
    /// </summary>
    public string BackendMutationErrorCaption
    {
        get => _backendMutationErrorCaption;
        private set => this.RaiseAndSetIfChanged(ref _backendMutationErrorCaption, value);
    }

    public bool CanBindNativeHarness
    {
        get => _canBindNativeHarness;
        private set => this.RaiseAndSetIfChanged(ref _canBindNativeHarness, value);
    }

    public bool CanUnbindBackend
    {
        get => _canUnbindBackend;
        private set => this.RaiseAndSetIfChanged(ref _canUnbindBackend, value);
    }

    /// <summary>
    /// True when the active selection is a direct conversation that can request
    /// explicit live-session termination via <see cref="EndSessionCommand"/>.
    /// </summary>
    public bool CanEndSession
    {
        get => _canEndSession;
        private set => this.RaiseAndSetIfChanged(ref _canEndSession, value);
    }

    public string AcpRuntimeCaption
    {
        get => _acpRuntimeCaption;
        private set => this.RaiseAndSetIfChanged(ref _acpRuntimeCaption, value);
    }

    public bool CanProbeAcp
    {
        get => _canProbeAcp;
        private set => this.RaiseAndSetIfChanged(ref _canProbeAcp, value);
    }

    public bool CanAuthenticateAcp
    {
        get => _canAuthenticateAcp;
        private set => this.RaiseAndSetIfChanged(ref _canAuthenticateAcp, value);
    }

    public bool CanLogoutAcp
    {
        get => _canLogoutAcp;
        private set => this.RaiseAndSetIfChanged(ref _canLogoutAcp, value);
    }

    public bool CanBindAcp
    {
        get => _canBindAcp;
        private set => this.RaiseAndSetIfChanged(ref _canBindAcp, value);
    }

    /// <summary>
    /// ACP config inputs (executable/args/expected identity). Visible for unbound
    /// configure and ACP reconfigure; hidden when Native Harness is active.
    /// </summary>
    public bool ShowAcpConfig
    {
        get => _showAcpConfig;
        private set => this.RaiseAndSetIfChanged(ref _showAcpConfig, value);
    }

    public string AcpExecutableDraft
    {
        get => _acpExecutableDraft;
        set => this.RaiseAndSetIfChanged(ref _acpExecutableDraft, value ?? string.Empty);
    }

    public string AcpArgumentsDraft
    {
        get => _acpArgumentsDraft;
        set => this.RaiseAndSetIfChanged(ref _acpArgumentsDraft, value ?? string.Empty);
    }

    public string AcpExpectedNameDraft
    {
        get => _acpExpectedNameDraft;
        set => this.RaiseAndSetIfChanged(ref _acpExpectedNameDraft, value ?? string.Empty);
    }

    public string AcpExpectedVersionDraft
    {
        get => _acpExpectedVersionDraft;
        set => this.RaiseAndSetIfChanged(ref _acpExpectedVersionDraft, value ?? string.Empty);
    }

    public string AcpAuthMethodDraft
    {
        get => _acpAuthMethodDraft;
        set => this.RaiseAndSetIfChanged(ref _acpAuthMethodDraft, value ?? string.Empty);
    }

    /// <summary>
    /// Command to select a context policy level from the selector index.
    /// </summary>
    public ReactiveCommand<int, Unit> SetContextPolicyFromSelectorCommand { get; }

    /// <summary>
    /// Command to clear the session policy override for the active direct conversation.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ClearContextPolicyOverrideCommand { get; }

    /// <summary>
    /// Binds the active direct agent to Native Harness via the production presenter.
    /// </summary>
    public ReactiveCommand<Unit, Unit> BindNativeHarnessCommand { get; }

    /// <summary>
    /// Unbinds the active direct agent's backend via the production presenter.
    /// </summary>
    public ReactiveCommand<Unit, Unit> UnbindBackendCommand { get; }

    /// <summary>
    /// Explicit live-session termination for the active direct conversation.
    /// Backed by <see cref="IAgentSessionService.EndAsync"/>; not channel-scoped.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EndSessionCommand { get; }

    /// <summary>
    /// Binds the active direct agent to ACP using the draft runtime identity fields.
    /// </summary>
    public ReactiveCommand<Unit, Unit> BindAcpCommand { get; }

    /// <summary>
    /// Probes the durable ACP binding (initialize + identity; no prompt session).
    /// </summary>
    public ReactiveCommand<Unit, Unit> ProbeAcpCommand { get; }

    /// <summary>
    /// Authenticates the ACP agent with the selected advertised method id.
    /// </summary>
    public ReactiveCommand<Unit, Unit> AuthenticateAcpCommand { get; }

    /// <summary>
    /// Capability-gated ACP logout for the active agent binding.
    /// </summary>
    public ReactiveCommand<Unit, Unit> LogoutAcpCommand { get; }

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
        IAgentContextSessionPolicyService sessionPolicyService,
        IAgentRouter? agentRouter = null)
        : this(
            state,
            actorCatalog,
            conversationStore,
            panelHost,
            executionCoordinator,
            sessionPolicyService,
            new TownhallConversationUiState(),
            persistenceBridge: null,
            persistenceService: null,
            agentRouter: agentRouter,
            backendSelectionService: null,
            backendBindingPresenter: null)
    {
    }

    internal TownhallViewModel(
        TownhallState state,
        IActorCatalog actorCatalog,
        IConversationStore conversationStore,
        IAgentPanelHost panelHost,
        IAgentExecutionCoordinator executionCoordinator,
        IAgentContextSessionPolicyService sessionPolicyService,
        TownhallConversationUiState conversationUiState,
        IConversationWorkspacePersistenceBridge? persistenceBridge,
        ConversationPersistenceService? persistenceService,
        IAgentRouter? agentRouter = null,
        IAgentActorBackendSelectionService? backendSelectionService = null,
        AgentBackendBindingPresenter? backendBindingPresenter = null,
        IAgentSessionService? sessionService = null,
        AgentTransparencyManagementViewModel? transparencyManagement = null)
    {
        _ = persistenceService;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _executionCoordinator = executionCoordinator ?? throw new ArgumentNullException(nameof(executionCoordinator));
        _sessionPolicyService = sessionPolicyService
            ?? throw new ArgumentNullException(nameof(sessionPolicyService));
        _backendSelectionService = backendSelectionService;
        _backendBindingPresenter = backendBindingPresenter;
        _agentRouter = agentRouter;
        _sessionService = sessionService;
        _transparencyManagement = transparencyManagement;
        _conversationUiState = conversationUiState ?? throw new ArgumentNullException(nameof(conversationUiState));
        _persistenceBridge = persistenceBridge;

        if (_backendBindingPresenter is not null)
        {
            _backendBindingPresenter.BindingChanged += OnBackendBindingChanged;
        }
        else if (_backendSelectionService is not null)
        {
            _backendSelectionService.BindingChanged += OnBackendBindingChanged;
        }

        if (_sessionService is not null)
        {
            // Refresh End Session availability on lifecycle transitions (admit, end, ready).
            _sessionEventsSubscription = _sessionService.Events.Subscribe(_ => RefreshCanEndSession());
        }

        _conversationStore.EntryAppended += OnConversationEntryAppended;
        _panelHost.Panels.CollectionChanged += OnAgentPanelsCollectionChanged;
        foreach (var panel in _panelHost.Panels)
        {
            panel.PropertyChanged += OnAgentPanelPropertyChanged;
        }

        // Initialize explicit session seed state
        InitializeSessionState();
        PublishMemoryTownhallContext();

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
        SetContextPolicyFromSelectorCommand = ReactiveCommand.Create<int>(ApplyContextPolicySelection);
        ClearContextPolicyOverrideCommand = ReactiveCommand.Create(ClearActiveContextPolicyOverride);
        BindNativeHarnessCommand = ReactiveCommand.Create(ExecuteBindNativeHarness);
        UnbindBackendCommand = ReactiveCommand.Create(ExecuteUnbindBackend);
        EndSessionCommand = ReactiveCommand.CreateFromTask(ExecuteEndSessionAsync);
        BindAcpCommand = ReactiveCommand.Create(ExecuteBindAcp);
        ProbeAcpCommand = ReactiveCommand.CreateFromTask(ExecuteProbeAcpAsync);
        AuthenticateAcpCommand = ReactiveCommand.CreateFromTask(ExecuteAuthenticateAcpAsync);
        LogoutAcpCommand = ReactiveCommand.CreateFromTask(ExecuteLogoutAcpAsync);
        UpdateDirectSendBusyTracking();
        RefreshCanEndSession();
    }

    private void ExecuteBindNativeHarness()
    {
        if (_activeBackendActorId is not { } actorId)
        {
            return;
        }

        if (_backendBindingPresenter is not null)
        {
            _ = _backendBindingPresenter.TryBindNativeHarness(actorId);
            RefreshActiveBackendBindingProjection();
            return;
        }

        _backendSelectionService?.TryBindNativeHarness(actorId);
        RefreshActiveBackendBindingProjection();
    }

    private void ExecuteUnbindBackend()
    {
        if (_activeBackendActorId is not { } actorId)
        {
            return;
        }

        if (_backendBindingPresenter is not null)
        {
            _ = _backendBindingPresenter.TryUnbind(actorId);
            RefreshActiveBackendBindingProjection();
            return;
        }

        // Fallback without presenter: revision-aware unbind is unavailable.
        RefreshActiveBackendBindingProjection();
    }

    private async Task ExecuteEndSessionAsync()
    {
        // Capture ownership before await; navigation must not redirect end effects.
        if (_state.ActiveConversationId is not { } sourceConversationId)
        {
            return;
        }

        if (!_conversationStore.TryGet(sourceConversationId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            return;
        }

        if (_sessionService is null)
        {
            return;
        }

        var result = await _sessionService.EndAsync(sourceConversationId).ConfigureAwait(true);
        if (result.Status == AgentSessionEndStatus.AcknowledgementIndeterminate)
        {
            var author = ResolveDirectPeerActorId(conversation);
            // Attempt correlation is required for live indeterminate attempts so repeated
            // projection of the same attempt is exactly once and distinct attempts differ.
            var correlation = result.AttemptCorrelation
                ?? throw new InvalidOperationException(
                    "Indeterminate termination result is missing attempt correlation.");
            AgentConversationEventProjection.ProjectTerminationIndeterminate(
                _conversationStore,
                sourceConversationId,
                author,
                result.Reason
                ?? "Backend acknowledgement timed out. Retry is available. Provider termination is not claimed.",
                correlation);
        }

        RefreshCanEndSession();
    }

    private void RefreshCanEndSession()
    {
        if (_sessionService is null
            || _state.ActiveConversationId is not { } activeId
            || !_conversationStore.TryGet(activeId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            CanEndSession = false;
            return;
        }

        // True only when this direct conversation owns a live session that can be ended.
        // Channels, direct without ownership, and successfully ended sessions are false.
        // Ending (including after indeterminate ack) remains true so the user can retry.
        var snapshot = _sessionService.TryGetSessionSnapshot(activeId);
        CanEndSession = snapshot is not null && snapshot.Status != AgentSessionStatus.Ended;
    }

    private void ExecuteBindAcp()
    {
        if (_activeBackendActorId is not { } actorId || _backendBindingPresenter is null)
        {
            return;
        }

        try
        {
            var args = string.IsNullOrWhiteSpace(AcpArgumentsDraft)
                ? Array.Empty<string>()
                : AcpArgumentsDraft.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var runtime = new AcpRuntimeIdentity(AcpExecutableDraft.Trim(), args);
            _ = _backendBindingPresenter.TryBindAcpRuntime(
                actorId,
                runtime,
                AcpExpectedNameDraft.Trim(),
                AcpExpectedVersionDraft.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Presenter records typed failures for durable path; surface validation here.
            BackendMutationErrorCaption = ex.Message;
        }

        RefreshActiveBackendBindingProjection();
    }

    private async Task ExecuteProbeAcpAsync()
    {
        if (_activeBackendActorId is not { } actorId || _backendBindingPresenter is null)
        {
            return;
        }

        await _backendBindingPresenter.ProbeAcpAsync(actorId).ConfigureAwait(true);
        RefreshActiveBackendBindingProjection();
    }

    private async Task ExecuteAuthenticateAcpAsync()
    {
        if (_activeBackendActorId is not { } actorId || _backendBindingPresenter is null)
        {
            return;
        }

        var methodId = string.IsNullOrWhiteSpace(AcpAuthMethodDraft)
            ? (_backendBindingPresenter.GetSnapshot(actorId).AdvertisedAuthMethodIds.FirstOrDefault() ?? string.Empty)
            : AcpAuthMethodDraft.Trim();
        await _backendBindingPresenter.AuthenticateAcpAsync(actorId, methodId).ConfigureAwait(true);
        RefreshActiveBackendBindingProjection();
    }

    private async Task ExecuteLogoutAcpAsync()
    {
        if (_activeBackendActorId is not { } actorId || _backendBindingPresenter is null)
        {
            return;
        }

        await _backendBindingPresenter.LogoutAcpAsync(actorId).ConfigureAwait(true);
        RefreshActiveBackendBindingProjection();
    }

    private void OnBackendBindingChanged(AgentActorBackendBindingChangedEvent change)
    {
        if (_activeBackendActorId is { } active && active == change.ActorId)
        {
            RefreshActiveBackendBindingProjection();
        }
    }

    private async Task SendMessageAsync()
    {
        // Capture the exact raw source draft before any await. Routing uses the
        // trimmed payload; draft clearing compares ordinal-exactly against the raw
        // snapshot so whitespace-only newer edits are preserved.
        var rawDraftSnapshot = DraftText ?? string.Empty;
        var submittedPayload = rawDraftSnapshot.Trim();
        if (string.IsNullOrEmpty(submittedPayload))
        {
            return;
        }

        if (_state.ActiveChannelId is not null)
        {
            if (_agentRouter is not null
                && _state.ActiveConversationId is { } channelConversationId
                && _conversationStore.TryGet(channelConversationId, out var channelConversation)
                && channelConversation.Kind == ConversationKind.Channel
                && ContainsCatalogMention(submittedPayload))
            {
                // Capture source ownership before await; navigation must not clear another draft.
                var sourceConversationId = channelConversationId;
                var routeResult = await _agentRouter.RouteAndExecuteFromConversationAsync(
                    sourceConversationId,
                    submittedPayload);
                TryClearDraftAfterRoute(routeResult, sourceConversationId, rawDraftSnapshot);
                RefreshCanEndSession();
                return;
            }

            LogActivity(
                entryKind: ConversationEntryKind.UserChat,
                content: submittedPayload,
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
            // Capture source ownership before await; navigation must not clear another draft.
            var sourceConversationId = activeConversationId;
            var routeResult = await _agentRouter.RouteAndExecuteAsync(panel.PanelId, submittedPayload);
            TryClearDraftAfterRoute(routeResult, sourceConversationId, rawDraftSnapshot);
            RefreshCanEndSession();
            return;
        }

        var result = await _executionCoordinator.SendAsync(panel.PanelId, submittedPayload);
        if (result is not null)
        {
            ClearActiveConversationDraft();
        }

        RefreshCanEndSession();
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

        // Apply channel/direct side effects before publishing ActiveConversationId so
        // views observing selection see consistent header and input context.
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
        }
        else if (conversation.Kind == ConversationKind.Direct)
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

        ActiveConversationId = conversationId;

        if (previousId != conversationId)
        {
            DraftText = _conversationUiState.GetDraft(conversationId);
            NotifyPresentationPersisted();
        }

        UpdateActiveConversationDisplayContext();
    }

    private void TryClearDraftAfterRoute(
        RouteResult routeResult,
        ConversationId sourceConversationId,
        string rawDraftSnapshot)
    {
        if (!ShouldClearDraftAfterRoute(routeResult, sourceConversationId))
        {
            return;
        }

        ClearSourceConversationDraftIfUnchanged(sourceConversationId, rawDraftSnapshot);
    }

    /// <summary>
    /// Clears only the captured source conversation's draft when its current value is
    /// ordinal-exactly equal to the raw pre-await snapshot. Never trims for the
    /// comparison: whitespace-only newer edits survive. Never rewrites the currently
    /// active conversation merely because it became active while another routed send
    /// was in flight.
    /// </summary>
    private void ClearSourceConversationDraftIfUnchanged(
        ConversationId sourceConversationId,
        string rawDraftSnapshot)
    {
        var sourceIsActive = _state.ActiveConversationId == sourceConversationId;
        var currentSourceDraft = sourceIsActive
            ? DraftText
            : _conversationUiState.GetDraft(sourceConversationId);

        if (!string.Equals(currentSourceDraft, rawDraftSnapshot, StringComparison.Ordinal))
        {
            return;
        }

        _conversationUiState.ClearDraft(sourceConversationId);
        if (sourceIsActive)
        {
            DraftText = string.Empty;
        }

        NotifyPresentationPersisted();
    }

    private bool ShouldClearDraftAfterRoute(RouteResult routeResult, ConversationId sourceConversationId)
    {
        if (routeResult.ExecutionResult is not { } executionResult)
        {
            return false;
        }

        if (routeResult.Request is { IsDirectSend: true })
        {
            return HasCorrelatedVisibleOutcome(sourceConversationId, executionResult.Run.Id);
        }

        if (routeResult.Success)
        {
            return HasCorrelatedVisibleOutcome(sourceConversationId, executionResult.Run.Id);
        }

        return HasCorrelatedVisibleOutcome(sourceConversationId, executionResult.Run.Id);
    }

    private bool HasCorrelatedVisibleOutcome(ConversationId conversationId, ExecutionRunId runId)
    {
        if (!_conversationStore.TryGet(conversationId, out var conversation))
        {
            return false;
        }

        var correlation = ConversationEntryCorrelationId.FromValue(runId.Value);
        return conversation.Entries.Any(e => e.CorrelationId == correlation
            && (e.Kind == ConversationEntryKind.RoutingFailure
                || e.Kind == ConversationEntryKind.ExecutionFailure
                || e.Kind == ConversationEntryKind.UserChat
                || (e.Kind == ConversationEntryKind.SystemNotification
                    && e.Content.StartsWith("zaide-route|v1|", StringComparison.Ordinal))));
    }

    private static bool ContainsCatalogMention(string draft)
    {
        foreach (var token in draft.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith('@'))
            {
                return true;
            }
        }

        return false;
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
            lock (_directNavSync)
            {
                var item = DirectNavItems.FirstOrDefault(i => i.ConversationId == conversation.Id);
                if (item is not null)
                {
                    item.HasUnread = isUnread;
                }
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

    private AgentPanelState EnsurePanelForDirectConversation(Conversation conversation)
    {
        var peerActorId = ResolveDirectPeerActorId(conversation);
        var panel = _panelHost.GetOrCreatePanelForActor(peerActorId);
        RefreshContextPolicyProjection(conversation.Id);
        return panel;
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

        // Conversation-keyed busy survives navigation (M7); no panel chrome projection (M8).
        IsDirectSendBusy = _executionCoordinator.IsConversationBusy(activeConversationId);

        Action<ConversationId, bool> busyHandler = (conversationId, isBusy) =>
        {
            if (conversationId == activeConversationId)
            {
                IsDirectSendBusy = isBusy;
            }
        };
        _executionCoordinator.ConversationBusyChanged += busyHandler;

        _directBusySubscription.Disposable = Disposable.Create(() =>
        {
            _executionCoordinator.ConversationBusyChanged -= busyHandler;
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

    internal void RefreshDirectNavItems()
    {
        var humanId = _actorCatalog.CanonicalHuman.Id;
        var selectedId = _state.ActiveConversationId;
        var items = _conversationStore.ListConversations()
            .Where(c => c.Kind == ConversationKind.Direct && c.Participants.Contains(humanId))
            .Select(c => CreateDirectNavItem(c, humanId, selectedId))
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_directNavSync)
        {
            DirectNavItems.Clear();
            foreach (var item in items)
            {
                DirectNavItems.Add(item);
            }
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

        var panel = _panelHost.Panels.FirstOrDefault(p => p.ConversationId == conversation.Id);
        var disclosureStatus = panel?.ContextDisclosureStatus ?? string.Empty;

        return new TownhallNavigationItem
        {
            ConversationId = conversation.Id,
            Kind = TownhallNavigationKind.Direct,
            Label = label,
            PeerActorId = peerId,
            IsSelected = selectedId.HasValue && selectedId.Value == conversation.Id,
            HasUnread = _conversationUiState.IsUnread(conversation),
            ContextDisclosureStatus = disclosureStatus
        };
    }

    private void UpdateDirectNavSelection()
    {
        var selectedId = _state.ActiveConversationId;
        lock (_directNavSync)
        {
            foreach (var item in DirectNavItems)
            {
                item.IsSelected = selectedId.HasValue && item.ConversationId == selectedId.Value;
                if (_conversationStore.TryGet(item.ConversationId, out var conversation))
                {
                    item.HasUnread = _conversationUiState.IsUnread(conversation);
                }
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

                if (isActive && _state.ActiveConversationId == conversationId)
                {
                    // Re-check activity after nav refresh: a concurrent switch must not
                    // append into a replaced Messages collection mid-navigation.
                    Messages.Add(TownhallEntryProjection.ToTownhallMessage(entry, _actorCatalog));
                }
            }
            else if (conversation.Kind == ConversationKind.Channel
                     && conversation.Id.TryGetChannelId(out var channelId))
            {
                // Channel presentation is conversation-owned: inactive routes must still
                // update the cached collection so returning later shows the entry once.
                EnsureChannelMessageProjected(channelId, entry);
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

        var timestamp = DateTimeOffset.UtcNow;
        var typedEntry = TownhallEntryProjection.CreateTypedEntry(
            entryKind,
            author,
            timestamp,
            content);

        // Presentation is owned by OnConversationEntryAppended (conversation-owned
        // cache update with authoritative entry-id dedupe). Do not mirror-add here.
        _conversationStore.AppendEntry(conversation.Id, typedEntry);
    }

    private void EnsureChannelMessageProjected(string channelId, ConversationEntry entry)
    {
        if (!_state.ChannelMessages.TryGetValue(channelId, out var channelMessages))
        {
            channelMessages = new ObservableCollection<TownhallMessage>();
            _state.ChannelMessages[channelId] = channelMessages;
        }

        if (channelMessages.Any(m => m.Id == entry.Id.Value))
        {
            return;
        }

        channelMessages.Add(TownhallEntryProjection.ToTownhallMessage(entry, _actorCatalog));
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

    private void UpdateActiveConversationDisplayContext()
    {
        string header;
        string placeholder;

        if (_state.ActiveConversationId is not { } activeId
            || !_conversationStore.TryGet(activeId, out var conversation))
        {
            header = string.Empty;
            placeholder = "Message...";
        }
        else if (conversation.Kind == ConversationKind.Channel
                 && activeId.TryGetChannelId(out var channelId))
        {
            var channel = _state.Channels.FirstOrDefault(c => c.Id == channelId);
            var name = channel?.Name ?? channelId;
            header = $"#{name}";
            placeholder = $"Message #{name}";
        }
        else if (conversation.Kind == ConversationKind.Direct)
        {
            header = ResolveDirectDisplayLabel(conversation);
            placeholder = $"Direct message with {header}";
        }
        else
        {
            header = string.Empty;
            placeholder = "Message...";
        }

        if (ActiveConversationHeaderLabel != header)
        {
            ActiveConversationHeaderLabel = header;
            this.RaisePropertyChanged(nameof(ActiveConversationHeaderLabel));
        }

        if (ActiveConversationInputPlaceholder != placeholder)
        {
            ActiveConversationInputPlaceholder = placeholder;
            this.RaisePropertyChanged(nameof(ActiveConversationInputPlaceholder));
        }

        RefreshActiveContextPolicyProjection();
        RefreshActiveBackendBindingProjection();
        RefreshCanEndSession();
    }

    private void RefreshActiveBackendBindingProjection()
    {
        if ((_backendBindingPresenter is null && _backendSelectionService is null)
            || _state.ActiveConversationId is not { } activeConversationId
            || !_conversationStore.TryGet(activeConversationId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            _activeBackendActorId = null;
            IsBackendBindingStatusVisible = false;
            BackendBindingLabel = string.Empty;
            BackendAuthStatusCaption = string.Empty;
            IsBackendDisconnected = false;
            BackendCapabilityCaption = string.Empty;
            BackendSettingsCaption = string.Empty;
            BackendMutationErrorCaption = string.Empty;
            CanBindNativeHarness = false;
            CanUnbindBackend = false;
            AcpRuntimeCaption = string.Empty;
            CanProbeAcp = false;
            CanAuthenticateAcp = false;
            CanLogoutAcp = false;
            CanBindAcp = false;
            ShowAcpConfig = false;
            return;
        }

        IsBackendBindingStatusVisible = true;
        var peer = conversation.Participants.All.FirstOrDefault(p => p != _actorCatalog.CanonicalHuman.Id);
        if (peer == default)
        {
            _activeBackendActorId = null;
            BackendBindingLabel = "Unbound";
            BackendAuthStatusCaption = "No agent participant";
            IsBackendDisconnected = true;
            BackendCapabilityCaption = string.Empty;
            BackendSettingsCaption = string.Empty;
            BackendMutationErrorCaption = string.Empty;
            CanBindNativeHarness = false;
            CanUnbindBackend = false;
            AcpRuntimeCaption = string.Empty;
            CanProbeAcp = false;
            CanAuthenticateAcp = false;
            CanLogoutAcp = false;
            CanBindAcp = false;
            ShowAcpConfig = false;
            return;
        }

        _activeBackendActorId = peer;

        if (_backendBindingPresenter is not null)
        {
            var projection = _backendBindingPresenter.BuildProjection(peer);
            BackendBindingLabel = projection.BackendLabel;
            BackendAuthStatusCaption = projection.AuthCaption;
            IsBackendDisconnected = projection.IsDisconnected;
            BackendCapabilityCaption = projection.CapabilityCaption;
            BackendSettingsCaption = projection.SettingsCaption;
            BackendMutationErrorCaption = projection.MutationErrorCaption ?? string.Empty;
            CanBindNativeHarness = projection.CanBindNativeHarness;
            CanUnbindBackend = projection.CanUnbind;
            AcpRuntimeCaption = projection.AcpRuntimeCaption ?? string.Empty;
            CanProbeAcp = projection.CanProbeAcp;
            CanAuthenticateAcp = projection.CanAuthenticate;
            CanLogoutAcp = projection.CanLogout;
            CanBindAcp = !projection.IsBound || projection.BackendId != AgentBackendIds.Acp;
            ShowAcpConfig = projection.ShowAcpConfig;
            if (projection.IsBound && projection.BackendId == AgentBackendIds.Acp)
            {
                if (!string.IsNullOrEmpty(projection.AcpExecutablePath))
                {
                    AcpExecutableDraft = projection.AcpExecutablePath;
                }

                if (!string.IsNullOrEmpty(projection.AcpArgumentsCaption)
                    && projection.AcpArgumentsCaption != "(no arguments)")
                {
                    AcpArgumentsDraft = projection.AcpArgumentsCaption;
                }

                if (!string.IsNullOrEmpty(projection.AcpExpectedAgentName))
                {
                    AcpExpectedNameDraft = projection.AcpExpectedAgentName!;
                }

                if (!string.IsNullOrEmpty(projection.AcpExpectedAgentVersion))
                {
                    AcpExpectedVersionDraft = projection.AcpExpectedAgentVersion!;
                }

                if (projection.AdvertisedAuthMethodIds.Count > 0
                    && string.IsNullOrWhiteSpace(AcpAuthMethodDraft))
                {
                    AcpAuthMethodDraft = projection.AdvertisedAuthMethodIds[0];
                }
            }

            return;
        }

        var snapshot = _backendSelectionService!.GetSnapshot(peer);
        BackendBindingLabel = snapshot.BackendLabel;
        BackendAuthStatusCaption = FormatAuthStateCaption(snapshot.AuthenticationState);
        IsBackendDisconnected = snapshot.IsDisconnected;
        BackendCapabilityCaption = snapshot.StatusCaption;
        BackendSettingsCaption = AgentBackendBindingWorkflowProjection.NativeSettingsCaption;
        BackendMutationErrorCaption = string.Empty;
        CanBindNativeHarness = !snapshot.IsBound || snapshot.BackendId != AgentBackendIds.NativeHarness;
        CanUnbindBackend = snapshot.IsBound;
        AcpRuntimeCaption = string.Empty;
        CanProbeAcp = false;
        CanAuthenticateAcp = false;
        CanLogoutAcp = false;
        CanBindAcp = !snapshot.IsBound || snapshot.BackendId != AgentBackendIds.Acp;
        ShowAcpConfig = !snapshot.IsBound || snapshot.BackendId == AgentBackendIds.Acp;
    }

    private static string FormatAuthStateCaption(AgentAuthenticationConnectionState authState) =>
        authState switch
        {
            AgentAuthenticationConnectionState.NotRequired => "Auth not required",
            AgentAuthenticationConnectionState.Authenticated => "Authenticated",
            AgentAuthenticationConnectionState.PendingUserAction => "Authentication required",
            AgentAuthenticationConnectionState.Disconnected => "Disconnected",
            AgentAuthenticationConnectionState.Failed => "Authentication failed",
            _ => authState.ToString(),
        };

    private void ApplyContextPolicySelection(int selectorIndex)
    {
        if (_state.ActiveConversationId is not { } activeConversationId
            || !_conversationStore.TryGet(activeConversationId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            return;
        }

        if (selectorIndex <= 0)
        {
            _sessionPolicyService.ClearSessionOverride(activeConversationId);
        }
        else if (TryMapSelectorIndexToPolicyLevel(selectorIndex, out var level))
        {
            _sessionPolicyService.TrySetSessionOverride(activeConversationId, level);
        }
        else
        {
            return;
        }

        RefreshContextPolicyProjection(activeConversationId);
    }

    private void ClearActiveContextPolicyOverride()
    {
        if (_state.ActiveConversationId is not { } activeConversationId)
        {
            return;
        }

        _sessionPolicyService.ClearSessionOverride(activeConversationId);
        RefreshContextPolicyProjection(activeConversationId);
    }

    private void RefreshActiveContextPolicyProjection()
    {
        if (_state.ActiveConversationId is not { } activeConversationId
            || !_conversationStore.TryGet(activeConversationId, out var conversation)
            || conversation.Kind != ConversationKind.Direct)
        {
            IsContextPolicySelectorVisible = false;
            ContextPolicyStatusCaption = string.Empty;
            IsContextPolicyOverrideActive = false;
            ContextPolicySelectorIndex = 0;
            return;
        }

        IsContextPolicySelectorVisible = true;
        RefreshContextPolicyProjection(activeConversationId);
    }

    private void RefreshContextPolicyProjection(ConversationId conversationId)
    {
        var policyState = _sessionPolicyService.GetPolicyState(conversationId);
        var selectorIndex = MapPolicyStateToSelectorIndex(policyState);

        ContextPolicyStatusCaption = policyState.StatusCaption;
        IsContextPolicyOverrideActive = policyState.IsOverrideActive;
        ContextPolicySelectorIndex = selectorIndex;

        var panel = _panelHost.Panels.FirstOrDefault(p => p.ConversationId == conversationId);
        if (panel is not null)
        {
            panel.ContextPolicyStatusCaption = policyState.StatusCaption;
            panel.IsContextPolicyOverrideActive = policyState.IsOverrideActive;
            panel.ContextPolicySelectorIndex = selectorIndex;
        }
    }

    private static int MapPolicyStateToSelectorIndex(AgentContextSessionPolicyState policyState)
    {
        if (!policyState.IsOverrideActive)
        {
            return 0;
        }

        return policyState.EffectiveLevel switch
        {
            AgentSessionContextPolicyLevel.Off => 1,
            AgentSessionContextPolicyLevel.Minimal => 2,
            AgentSessionContextPolicyLevel.Standard => 3,
            AgentSessionContextPolicyLevel.Detailed => 4,
            _ => 0,
        };
    }

    private static bool TryMapSelectorIndexToPolicyLevel(
        int selectorIndex,
        out AgentSessionContextPolicyLevel level)
    {
        switch (selectorIndex)
        {
            case 1:
                level = AgentSessionContextPolicyLevel.Off;
                return true;
            case 2:
                level = AgentSessionContextPolicyLevel.Minimal;
                return true;
            case 3:
                level = AgentSessionContextPolicyLevel.Standard;
                return true;
            case 4:
                level = AgentSessionContextPolicyLevel.Detailed;
                return true;
            default:
                level = default;
                return false;
        }
    }

    private string ResolveDirectDisplayLabel(Conversation conversation)
    {
        var humanId = _actorCatalog.CanonicalHuman.Id;
        var peer = conversation.Participants.All.FirstOrDefault(participant => participant != humanId);
        if (peer != default
            && _actorCatalog.TryGet(peer, out var actor)
            && !string.IsNullOrWhiteSpace(actor.DisplayName))
        {
            return actor.DisplayName;
        }

        if (peer != default)
        {
            return peer.Value;
        }

        return "Direct";
    }

    private void OnAgentPanelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.NewItems != null)
        {
            foreach (AgentPanelState panel in args.NewItems)
            {
                panel.PropertyChanged += OnAgentPanelPropertyChanged;
            }
        }

        if (args.OldItems != null)
        {
            foreach (AgentPanelState panel in args.OldItems)
            {
                panel.PropertyChanged -= OnAgentPanelPropertyChanged;
            }
        }
    }

    private void OnAgentPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentPanelState.ContextDisclosureStatus) && sender is AgentPanelState panel)
        {
            lock (_directNavSync)
            {
                var navItem = DirectNavItems.FirstOrDefault(i => i.ConversationId == panel.ConversationId);
                if (navItem != null)
                {
                    navItem.ContextDisclosureStatus = panel.ContextDisclosureStatus;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _conversationStore.EntryAppended -= OnConversationEntryAppended;
        _panelHost.Panels.CollectionChanged -= OnAgentPanelsCollectionChanged;
        foreach (var panel in _panelHost.Panels.ToArray())
        {
            panel.PropertyChanged -= OnAgentPanelPropertyChanged;
        }

        if (_backendBindingPresenter is not null)
        {
            _backendBindingPresenter.BindingChanged -= OnBackendBindingChanged;
        }
        else if (_backendSelectionService is not null)
        {
            _backendSelectionService.BindingChanged -= OnBackendBindingChanged;
        }

        _sessionEventsSubscription?.Dispose();
        _directBusySubscription.Dispose();
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
