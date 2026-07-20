using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// In-memory per-conversation Townhall UI state: draft text and last-read cursor.
/// Owned by Townhall presentation (not the conversation store). Phase 14 M5 is
/// session-only; M6 may persist the same field shape.
/// </summary>
internal sealed class TownhallConversationUiState
{
    private readonly Dictionary<ConversationId, string> _draftsByConversation = new();
    private readonly Dictionary<ConversationId, ConversationEntryId> _lastReadByConversation = new();

    public string GetDraft(ConversationId conversationId) =>
        _draftsByConversation.TryGetValue(conversationId, out var draft)
            ? draft
            : string.Empty;

    public void SetDraft(ConversationId conversationId, string? draft) =>
        _draftsByConversation[conversationId] = draft ?? string.Empty;

    public void ClearDraft(ConversationId conversationId) =>
        _draftsByConversation[conversationId] = string.Empty;

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
        _draftsByConversation.Clear();
        _lastReadByConversation.Clear();

        foreach (var pair in drafts)
        {
            if (string.IsNullOrEmpty(pair.Value))
            {
                continue;
            }

            _draftsByConversation[ConversationId.FromValue(pair.Key)] = pair.Value;
        }

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
        drafts = _draftsByConversation
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .ToDictionary(
                pair => pair.Key.Value,
                pair => pair.Value,
                StringComparer.Ordinal);

        lastReadEntryIds = _lastReadByConversation.ToDictionary(
            pair => pair.Key.Value,
            pair => pair.Value.Value,
            StringComparer.Ordinal);
    }
}
