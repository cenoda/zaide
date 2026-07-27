using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

using Zaide.Features.Agents.Domain;

/// <summary>
/// Read-only request for prior-conversation replay selection. The harness never
/// writes to <see cref="IConversationStore"/> through this seam.
/// </summary>
internal sealed class NativeHarnessPriorConversationReplayRequest
{
    public NativeHarnessPriorConversationReplayRequest(
        ConversationId conversationId,
        ConversationEntryId currentMessageEntryId,
        NativeHarnessPriorConversationReplayPolicy policy)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (currentMessageEntryId == default)
        {
            throw new ArgumentException(
                "Current message entry id is required.",
                nameof(currentMessageEntryId));
        }

        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        ConversationId = conversationId;
        CurrentMessageEntryId = currentMessageEntryId;
    }

    public ConversationId ConversationId { get; }

    public ConversationEntryId CurrentMessageEntryId { get; }

    public NativeHarnessPriorConversationReplayPolicy Policy { get; }
}
