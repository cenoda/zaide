using System;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Helpers for tests that need authoritative typed conversation entries on panels.
/// </summary>
internal static class AgentPanelTestSupport
{
    public static void AppendUserChat(
        IConversationStore store,
        AgentPanelState panel,
        string content,
        ExecutionRunId? runId = null)
    {
        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            DateTimeOffset.UtcNow,
            content,
            runId is null ? null : ExecutionRunCorrelation.ToEntryCorrelation(runId.Value));

        store.AppendEntry(panel.ConversationId, entry);
    }

    public static void AppendAssistantResponse(
        IConversationStore store,
        AgentPanelState panel,
        string content,
        ExecutionRunId? runId = null)
    {
        var entry = ConversationEntry.AssistantResponse(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            content,
            runId is null ? null : ExecutionRunCorrelation.ToEntryCorrelation(runId.Value));

        store.AppendEntry(panel.ConversationId, entry);
    }

    public static void AppendExecutionFailure(
        IConversationStore store,
        AgentPanelState panel,
        string content,
        ExecutionRunId? runId = null)
    {
        var entry = ConversationEntry.ExecutionFailure(
            ConversationEntryId.New(),
            panel.ActorId,
            DateTimeOffset.UtcNow,
            content,
            runId is null ? null : ExecutionRunCorrelation.ToEntryCorrelation(runId.Value));

        store.AppendEntry(panel.ConversationId, entry);
    }

    public static void SimulateDirectSendSuccess(
        IConversationStore store,
        AgentPanelState panel,
        string userMessage,
        string assistantResponse = "Hello back")
    {
        var runId = ExecutionRunId.New();
        AppendUserChat(store, panel, userMessage, runId);
        AppendAssistantResponse(store, panel, assistantResponse, runId);
        panel.Status = "Idle";
        panel.IsBusy = false;
    }

    public static void SimulateDirectSendError(
        IConversationStore store,
        AgentPanelState panel,
        string userMessage,
        string errorMessage = "Request failed")
    {
        var runId = ExecutionRunId.New();
        AppendUserChat(store, panel, userMessage, runId);
        AppendExecutionFailure(store, panel, errorMessage, runId);
        panel.Status = "Error";
        panel.IsBusy = false;
    }

    public static AgentPanelState CreatePanelState(
        string legacyId = "agent-test",
        string displayName = "Test Agent",
        string avatar = "avatar-test",
        IConversationStore? store = null)
    {
        store ??= Conversations.ConversationsTestSupport.CreateStore();
        var actor = new Actor(
            ActorId.PanelCustom(legacyId),
            ActorKind.Agent,
            legacyId,
            displayName,
            avatar);
        var conversation = store.CreateDirectConversation(ActorId.HumanUser, actor.Id);
        var projection = new AgentPanelOutputHistoryProjection(store, conversation.Id);
        return new AgentPanelState(actor, conversation.Id, projection.Lines);
    }
}
