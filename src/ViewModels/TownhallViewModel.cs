using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using Zaide.Models;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for the Townhall workspace.
/// Exposes channels, messages, agents, and draft state as reactive properties.
/// Commands: select channel, send message.
/// Uses simple in-memory sample data only.
/// Messages are stored per-channel in TownhallState.ChannelMessages.
/// </summary>
public class TownhallViewModel : ReactiveObject
{
    private readonly TownhallState _state;
    private string _draftText = string.Empty;

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
    public TownhallViewModel(TownhallState state)
    {
        _state = state;

        // Initialize sample data
        InitializeSampleData();

        // Setup reactive properties based on state
        Channels = _state.Channels;
        Agents = _state.Agents;

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

            // Ensure the channel has a messages list in the dictionary
            if (!_state.ChannelMessages.ContainsKey(_state.ActiveChannelId))
            {
                _state.ChannelMessages[_state.ActiveChannelId] = new ObservableCollection<TownhallMessage>();
            }

            var messagesList = _state.ChannelMessages[_state.ActiveChannelId];

            // Create new message
            var newMessage = new TownhallMessage
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = "user-1",
                SenderName = "User",
                SenderAvatar = "avatar-user",
                Content = draft,
                Timestamp = DateTimeOffset.UtcNow,
                Type = TownhallMessageType.Normal
            };

            // Add to active channel's messages collection (also adds to Messages since they're the same object)
            messagesList.Add(newMessage);

            // Clear draft after sending
            DraftText = string.Empty;
        });
    }

    private void InitializeSampleData()
    {
        // Create sample channels
        var townhallMain = new Channel { Id = "channel-1", Name = "townhall-main", IsPinned = true };
        var aiStatus = new Channel { Id = "channel-2", Name = "ai-status", IsPinned = false };
        var codebaseRefactoring = new Channel { Id = "channel-3", Name = "codebase-refactor", IsPinned = true };

        _state.Channels.Add(townhallMain);
        _state.Channels.Add(aiStatus);
        _state.Channels.Add(codebaseRefactoring);

        // Create sample messages for each channel (per-channel storage)
        var townhallMessages = new ObservableCollection<TownhallMessage>();
        var aiStatusMessages = new ObservableCollection<TownhallMessage>();
        var refactoringMessages = new ObservableCollection<TownhallMessage>();

        // Townhall main messages
        var message1 = new TownhallMessage
        {
            Id = "msg-1",
            SenderId = "user-1",
            SenderName = "User",
            SenderAvatar = "avatar-user",
            Content = "Welcome to the Townhall workspace!",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
            Type = TownhallMessageType.Normal
        };
        var message2 = new TownhallMessage
        {
            Id = "msg-2",
            SenderId = "agent-1",
            SenderName = "Zaide Agent",
            SenderAvatar = "avatar-agent",
            Content = "I can help you with code review and refactoring tasks.",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4),
            Type = TownhallMessageType.Normal
        };
        townhallMessages.Add(message1);
        townhallMessages.Add(message2);

        // AI status messages
        var aiMessage1 = new TownhallMessage
        {
            Id = "msg-ai-1",
            SenderId = "agent-1",
            SenderName = "Zaide Agent",
            SenderAvatar = "avatar-agent",
            Content = "System check complete. All systems nominal.",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3),
            Type = TownhallMessageType.Normal
        };
        aiStatusMessages.Add(aiMessage1);

        // Store per-channel message lists in state
        _state.ChannelMessages[townhallMain.Id] = townhallMessages;
        _state.ChannelMessages[aiStatus.Id] = aiStatusMessages;
        _state.ChannelMessages[codebaseRefactoring.Id] = refactoringMessages;

        // Set initial active channel (which also sets IsActive flags and Messages collection)
        ActiveChannelId = townhallMain.Id;

        // Create sample agents
        var user = new WorkspaceAgent { Id = "user-1", Name = "User", Avatar = "avatar-user", Role = "user", Status = AgentStatus.Active, HasWarning = false };
        var agent1 = new WorkspaceAgent { Id = "agent-1", Name = "Zaide Agent", Avatar = "avatar-agent", Role = "agent", Status = AgentStatus.Active, HasWarning = false };

        _state.Agents.Add(user);
        _state.Agents.Add(agent1);

        // Set initial draft text (syncs with state automatically via setter)
        DraftText = string.Empty;
    }
}
