using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Appends authoritative typed direct-conversation entries for panel execution.
/// </summary>
internal static class AgentPanelDirectConversationWriter
{
    public static ConversationEntry AppendUserMessage(
        IConversationStore conversationStore,
        AgentPanelState panel,
        ExecutionRunId runId,
        string userMessage)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            DateTimeOffset.UtcNow,
            userMessage,
            ExecutionRunCorrelation.ToEntryCorrelation(runId));

        conversationStore.AppendEntry(panel.ConversationId, entry);
        return entry;
    }

    public static ConversationEntry AppendAssistantResponse(
        IConversationStore conversationStore,
        AgentPanelState panel,
        ExecutionRunId runId,
        string assistantResponse)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.AssistantResponse(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            assistantResponse,
            ExecutionRunCorrelation.ToEntryCorrelation(runId));

        conversationStore.AppendEntry(panel.ConversationId, entry);
        return entry;
    }

    public static ConversationEntry AppendExecutionFailure(
        IConversationStore conversationStore,
        AgentPanelState panel,
        ExecutionRunId runId,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.ExecutionFailure(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            errorMessage,
            ExecutionRunCorrelation.ToEntryCorrelation(runId));

        conversationStore.AppendEntry(panel.ConversationId, entry);
        return entry;
    }

    public static ConversationEntry AppendRoutingFailure(
        IConversationStore conversationStore,
        AgentPanelState panel,
        ExecutionRunId runId,
        string failureReason)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        ArgumentNullException.ThrowIfNull(panel);

        var entry = ConversationEntry.RoutingFailure(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            failureReason,
            ExecutionRunCorrelation.ToEntryCorrelation(runId));

        conversationStore.AppendEntry(panel.ConversationId, entry);
        return entry;
    }
}
