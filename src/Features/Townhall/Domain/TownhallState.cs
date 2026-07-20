using System.Collections.ObjectModel;
using System.Collections.Generic;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Townhall.Domain;

/// <summary>
/// Holds the current session state for the Townhall workspace.
/// Messages are stored per-channel in a dictionary.
/// </summary>
public class TownhallState
{
    /// <summary>
    /// List of available channels.
    /// </summary>
    public ObservableCollection<Channel> Channels { get; } = new();

    /// <summary>
    /// ID of the currently active channel when a channel conversation is selected.
    /// </summary>
    public string? ActiveChannelId { get; set; }

    /// <summary>
    /// Authoritative presentation selection for the active conversation.
    /// </summary>
    public ConversationId? ActiveConversationId { get; set; }

    /// <summary>
    /// Messages indexed by channel ID. Each key maps to a list of messages for that channel.
    /// The current implementation maintains per-channel message history.
    /// </summary>
    public Dictionary<string, ObservableCollection<TownhallMessage>> ChannelMessages { get; } = new();

    /// <summary>
    /// List of workspace agents/users.
    /// </summary>
    public ObservableCollection<WorkspaceAgent> Agents { get; } = new();

    /// <summary>
    /// Current draft text being typed by the user.
    /// Synced with ViewModel.DraftText on change.
    /// </summary>
    public string DraftText { get; set; } = string.Empty;
}
