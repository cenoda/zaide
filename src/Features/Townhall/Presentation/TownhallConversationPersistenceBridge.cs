using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Conversations.Infrastructure;
using Zaide.Features.Townhall.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Townhall adapter for conversation workspace persistence (Phase 14 M6).
/// </summary>
internal sealed class TownhallConversationPersistenceBridge : IConversationWorkspacePersistenceBridge
{
    private readonly TownhallState _state;
    private readonly TownhallConversationUiState _uiState;

    public TownhallConversationPersistenceBridge(
        TownhallState state,
        TownhallConversationUiState uiState)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
    }

    public event Action? PresentationStateChanged;

    public bool WasRestoredFromPersistence { get; private set; }

    public string? RestoredActiveConversationId { get; private set; }

    public void ApplyRestoredSnapshot(ConversationWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        WasRestoredFromPersistence = true;
        RestoredActiveConversationId = snapshot.ActiveConversationId;

        _state.Channels.Clear();
        foreach (var channelRow in snapshot.Channels)
        {
            _state.Channels.Add(new Channel
            {
                Id = channelRow.Id,
                Name = channelRow.Name,
                IsPinned = channelRow.Pinned
            });
        }

        _uiState.ImportMaps(snapshot.Drafts, snapshot.LastReadEntryIds);
    }

    public ConversationWorkspaceSnapshot CapturePresentationSnapshot(
        IReadOnlyList<Conversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        var channels = _state.Channels
            .Select(channel => new PersistedChannelSnapshot
            {
                Id = channel.Id,
                Name = channel.Name,
                Pinned = channel.IsPinned
            })
            .ToList();

        _uiState.ExportMaps(
            out var drafts,
            out var lastReadEntryIds);

        return ConversationSnapshotSerializer.FromDomain(
            conversations,
            channels,
            _state.ActiveConversationId?.Value,
            drafts,
            lastReadEntryIds);
    }

    public void NotifyPresentationStateChanged() =>
        PresentationStateChanged?.Invoke();
}
