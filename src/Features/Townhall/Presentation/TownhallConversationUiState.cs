using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// In-memory per-conversation Townhall UI state: draft text (shared contract) and
/// last-read cursor. Owned by Townhall presentation (not the conversation store).
/// </summary>
internal sealed class TownhallConversationUiState
{
    private readonly IConversationDraftState _drafts;
    private readonly Dictionary<ConversationId, ConversationEntryId> _lastReadByConversation = new();

    public TownhallConversationUiState()
        : this(new Zaide.Features.Conversations.Application.ConversationDraftState())
    {
    }

    public TownhallConversationUiState(IConversationDraftState drafts)
    {
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
    }

    public string GetDraft(ConversationId conversationId) =>
        _drafts.GetDraft(conversationId);

    public void SetDraft(ConversationId conversationId, string? draft) =>
        _drafts.SetDraft(conversationId, draft);

    public void ClearDraft(ConversationId conversationId) =>
        _drafts.ClearDraft(conversationId);

    public ConversationEntryId? GetLastReadEntryId(ConversationId conversationId) =>
        _lastReadByConversation.TryGetValue(conversationId, out var entryId)
            ? entryId
            : null;

    public void SetLastReadEntryId(ConversationId conversationId, ConversationEntryId? entryId)
    {
        if (entryId is null)
        {
            _lastReadByConversation.Remove(conversationId);
            return;
        }

        _lastReadByConversation[conversationId] = entryId.Value;
    }

    /// <summary>
    /// True when the conversation has entries and the last-read cursor is not
    /// the latest entry id (including never-read conversations with history).
    /// </summary>
    public bool IsUnread(Conversation conversation)
    {
        if (conversation.Entries.Count == 0)
        {
            return false;
        }

        var latestId = conversation.Entries[^1].Id;
        if (!_lastReadByConversation.TryGetValue(conversation.Id, out var lastRead))
        {
            return true;
        }

        return lastRead != latestId;
    }

    public void ImportMaps(
        IReadOnlyDictionary<string, string> drafts,
        IReadOnlyDictionary<string, string> lastReadEntryIds)
    {
        _drafts.ImportDrafts(drafts);
        _lastReadByConversation.Clear();

        foreach (var pair in lastReadEntryIds)
        {
            _lastReadByConversation[ConversationId.FromValue(pair.Key)] =
                ConversationEntryId.FromValue(pair.Value);
        }
    }

    public void ExportMaps(
        out IReadOnlyDictionary<string, string> drafts,
        out IReadOnlyDictionary<string, string> lastReadEntryIds)
    {
        drafts = _drafts.ExportNonEmptyDrafts();

        lastReadEntryIds = _lastReadByConversation.ToDictionary(
            pair => pair.Key.Value,
            pair => pair.Value.Value,
            StringComparer.Ordinal);
    }
}
