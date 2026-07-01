using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using Zaide.Models;

namespace Zaide.ViewModels;

public class TownhallViewModel : ReactiveObject
{
    private readonly Dictionary<string, ObservableCollection<TownhallMessage>> _messagesByChannel = new();
    private readonly Dictionary<string, string> _draftByChannel = new();
    private string _activeChannelId = string.Empty;
    private string _draftText = string.Empty;

    public ObservableCollection<Channel> Channels { get; } = new();
    public ObservableCollection<TownhallMessage> Messages { get; } = new();
    public ObservableCollection<WorkspaceAgent> Agents { get; } = new();

    public string ActiveChannelId
    {
        get => _activeChannelId;
        private set => this.RaiseAndSetIfChanged(ref _activeChannelId, value);
    }

    public string DraftText
    {
        get => _draftText;
        set => this.RaiseAndSetIfChanged(ref _draftText, value);
    }

    public TownhallViewModel()
    {
        SeedData();
        if (Channels.Count > 0)
            SelectChannel(Channels[0].Id);
    }

    public void SelectChannel(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            return;

        if (!string.IsNullOrEmpty(ActiveChannelId))
            _draftByChannel[ActiveChannelId] = DraftText;

        ActiveChannelId = channelId;

        foreach (var channel in Channels)
            channel.IsActive = channel.Id == channelId;

        this.RaisePropertyChanged(nameof(Channels));

        Messages.Clear();
        if (_messagesByChannel.TryGetValue(channelId, out var channelMessages))
        {
            foreach (var message in channelMessages.OrderBy(m => m.Timestamp))
                Messages.Add(message);
        }

        DraftText = _draftByChannel.TryGetValue(channelId, out var draft) ? draft : string.Empty;
    }

    public void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(ActiveChannelId) || string.IsNullOrWhiteSpace(DraftText))
            return;

        if (!_messagesByChannel.TryGetValue(ActiveChannelId, out var channelMessages))
        {
            channelMessages = new ObservableCollection<TownhallMessage>();
            _messagesByChannel[ActiveChannelId] = channelMessages;
        }

        var message = new TownhallMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            SenderId = "user-1",
            Content = DraftText.Trim(),
            Timestamp = DateTimeOffset.Now
        };

        channelMessages.Add(message);
        Messages.Add(message);
        DraftText = string.Empty;
        _draftByChannel[ActiveChannelId] = string.Empty;
    }

    private void SeedData()
    {
        Channels.Add(new Channel { Id = "townhall-main", Name = "# townhall-main", IsActive = true });
        Channels.Add(new Channel { Id = "ai-status", Name = "# ai-status", IsActive = false });
        Channels.Add(new Channel { Id = "build-watch", Name = "# build-watch", IsActive = false });

        _messagesByChannel["townhall-main"] = new ObservableCollection<TownhallMessage>
        {
            new() { Id = "m1", SenderId = "agent-1", Content = "Refactor 3 kickoff started.", Timestamp = DateTimeOffset.Now.AddMinutes(-15) },
            new() { Id = "m2", SenderId = "user-1", Content = "Keep editor always visible on the right.", Timestamp = DateTimeOffset.Now.AddMinutes(-12) }
        };
        _messagesByChannel["ai-status"] = new ObservableCollection<TownhallMessage>
        {
            new() { Id = "m3", SenderId = "agent-2", Content = "Indexing workspace complete.", Timestamp = DateTimeOffset.Now.AddMinutes(-10) }
        };
        _messagesByChannel["build-watch"] = new ObservableCollection<TownhallMessage>
        {
            new() { Id = "m4", SenderId = "agent-1", Content = "Build green on main.", Timestamp = DateTimeOffset.Now.AddMinutes(-8) }
        };

        Agents.Add(new WorkspaceAgent { Id = "user-1", Name = "You", Role = WorkspaceRole.User, Status = WorkspaceAgentStatus.Active });
        Agents.Add(new WorkspaceAgent { Id = "agent-1", Name = "Builder Agent", Role = WorkspaceRole.Agent, Status = WorkspaceAgentStatus.Busy });
        Agents.Add(new WorkspaceAgent { Id = "agent-2", Name = "Review Agent", Role = WorkspaceRole.Agent, Status = WorkspaceAgentStatus.Idle });
    }
}
