using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Dual-writes authoritative typed direct-conversation entries and projects
/// them into the panel's legacy <see cref="AgentPanelState.OutputHistory"/>.
/// </summary>
internal static class AgentPanelDirectConversationWriter
{
    public static ConversationEntry AppendUserMessage(
        IConversationStore conversationStore,
        AgentPanelState panel,
        string userMessage)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            DateTimeOffset.UtcNow,
            userMessage);

        conversationStore.AppendEntry(panel.ConversationId, entry);
        panel.OutputHistory.Add(AgentPanelEntryProjection.ToOutputHistoryLine(entry));
        return entry;
    }

    public static ConversationEntry AppendAssistantResponse(
        IConversationStore conversationStore,
        AgentPanelState panel,
        string assistantResponse)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.AssistantResponse(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            assistantResponse);

        conversationStore.AppendEntry(panel.ConversationId, entry);
        panel.OutputHistory.Add(AgentPanelEntryProjection.ToOutputHistoryLine(entry));
        return entry;
    }

    public static ConversationEntry AppendExecutionFailure(
        IConversationStore conversationStore,
        AgentPanelState panel,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.ExecutionFailure(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            errorMessage);

        conversationStore.AppendEntry(panel.ConversationId, entry);
        panel.OutputHistory.Add(AgentPanelEntryProjection.ToOutputHistoryLine(entry));
        return entry;
    }
}
