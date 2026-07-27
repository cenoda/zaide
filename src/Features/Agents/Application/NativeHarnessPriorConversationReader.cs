using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Read-only prior-conversation replay selection from <see cref="IConversationStore"/>.
/// </summary>
internal sealed class NativeHarnessPriorConversationReader : INativeHarnessPriorConversationReader
{
    private readonly IConversationStore _conversationStore;

    public NativeHarnessPriorConversationReader(IConversationStore conversationStore)
    {
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
    }

    public IReadOnlyList<NativeHarnessPriorConversationReplayEntry> SelectReplayEntries(
        NativeHarnessPriorConversationReplayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_conversationStore.TryGet(request.ConversationId, out var conversation))
        {
            return Array.Empty<NativeHarnessPriorConversationReplayEntry>();
        }

        var currentIndex = -1;
        for (var index = 0; index < conversation.Entries.Count; index++)
        {
            if (conversation.Entries[index].Id == request.CurrentMessageEntryId)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return Array.Empty<NativeHarnessPriorConversationReplayEntry>();
        }

        var selected = new List<NativeHarnessPriorConversationReplayEntry>();
        var remainingTokens = request.Policy.MaxTokenBudget;
        var entryCount = 0;

        for (var index = currentIndex - 1; index >= 0; index--)
        {
            var entry = conversation.Entries[index];
            if (!request.Policy.IncludedKinds.Contains(entry.Kind))
            {
                continue;
            }

            if (entryCount >= request.Policy.MaxEntryCount)
            {
                break;
            }

            var estimatedTokens = AgentContextTokenEstimator.Estimate(entry.Content);
            if (estimatedTokens > remainingTokens)
            {
                break;
            }

            selected.Add(new NativeHarnessPriorConversationReplayEntry(
                entry.Id,
                entry.Kind,
                entry.Author,
                entry.Content,
                estimatedTokens));

            remainingTokens -= estimatedTokens;
            entryCount++;
        }

        selected.Reverse();
        return selected;
    }
}
