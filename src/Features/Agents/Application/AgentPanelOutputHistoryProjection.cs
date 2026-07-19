using System;
using System.Collections.ObjectModel;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Read-only presentation projection from authoritative direct-conversation
/// entries to the existing Agent Panel output string protocol.
/// </summary>
internal sealed class AgentPanelOutputHistoryProjection : IDisposable
{
    private readonly IConversationStore _conversationStore;
    private readonly ConversationId _conversationId;
    private readonly ObservableCollection<string> _backing = new();

    public AgentPanelOutputHistoryProjection(
        IConversationStore conversationStore,
        ConversationId conversationId)
    {
        _conversationStore = conversationStore
            ?? throw new ArgumentNullException(nameof(conversationStore));
        if (conversationId == default)
        {
            throw new ArgumentException(
                "Conversation id is required.",
                nameof(conversationId));
        }

        _conversationId = conversationId;
        Lines = new ReadOnlyObservableCollection<string>(_backing);

        SyncExistingEntries();
        _conversationStore.EntryAppended += OnEntryAppended;
    }

    public ReadOnlyObservableCollection<string> Lines { get; }

    private void SyncExistingEntries()
    {
        if (!_conversationStore.TryGet(_conversationId, out var conversation))
        {
            return;
        }

        foreach (var entry in conversation.Entries)
        {
            TryAppendProjectedLine(entry);
        }
    }

    private void OnEntryAppended(ConversationId conversationId, ConversationEntry entry)
    {
        if (conversationId != _conversationId)
        {
            return;
        }

        TryAppendProjectedLine(entry);
    }

    private void TryAppendProjectedLine(ConversationEntry entry)
    {
        if (AgentPanelEntryProjection.TryToOutputHistoryLine(entry, out var line))
        {
            _backing.Add(line);
        }
    }

    public void Dispose()
    {
        _conversationStore.EntryAppended -= OnEntryAppended;
    }
}
