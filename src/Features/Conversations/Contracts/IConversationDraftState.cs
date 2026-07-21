using System.Collections.Generic;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Contracts;

/// <summary>
/// Per-<see cref="ConversationId"/> draft text shared by Townhall and Agent Panel
/// thin-host surfaces. Authoritative for M5/M6 persistence field shape.
/// </summary>
public interface IConversationDraftState
{
    string GetDraft(ConversationId conversationId);

    void SetDraft(ConversationId conversationId, string? draft);

    void ClearDraft(ConversationId conversationId);

    void ImportDrafts(IReadOnlyDictionary<string, string> drafts);

    IReadOnlyDictionary<string, string> ExportNonEmptyDrafts();
}
