using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Application;

/// <summary>
/// Application-lifetime per-conversation draft map. Shared by Townhall presentation
/// and Agent Panel thin hosts (Phase 14 M7).
/// </summary>
internal sealed class ConversationDraftState : IConversationDraftState
{
    private readonly Dictionary<ConversationId, string> _draftsByConversation = new();

    public string GetDraft(ConversationId conversationId) =>
        _draftsByConversation.TryGetValue(conversationId, out var draft)
            ? draft
            : string.Empty;

    public void SetDraft(ConversationId conversationId, string? draft) =>
        _draftsByConversation[conversationId] = draft ?? string.Empty;

    public void ClearDraft(ConversationId conversationId) =>
        _draftsByConversation[conversationId] = string.Empty;

    public void ImportDrafts(IReadOnlyDictionary<string, string> drafts)
    {
        _draftsByConversation.Clear();
        foreach (var pair in drafts)
        {
            if (string.IsNullOrEmpty(pair.Value))
            {
                continue;
            }

            _draftsByConversation[ConversationId.FromValue(pair.Key)] = pair.Value;
        }
    }

    public IReadOnlyDictionary<string, string> ExportNonEmptyDrafts() =>
        _draftsByConversation
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .ToDictionary(
                pair => pair.Key.Value,
                pair => pair.Value,
                StringComparer.Ordinal);
}
