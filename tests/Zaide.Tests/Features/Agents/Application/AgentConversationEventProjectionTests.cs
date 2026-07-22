using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 15 M3b-2 unit tests for normalized event to conversation entry projection.
/// </summary>
public sealed class AgentConversationEventProjectionTests
{
    private static AgentEvent CreateUserAdmittedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ConversationEntryId messageEntryId,
        string text,
        long sequence = 1)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendId.FromValue("backend:test"),
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideExecuted,
            AgentEventKind.UserMessageAdmitted,
            new AgentMessagePayload(messageEntryId, text));
    }

    private static AgentEvent CreateAssistantCompletedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ConversationEntryId messageEntryId,
        string text,
        long sequence = 2)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendId.FromValue("backend:test"),
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.BackendExecutedAndReported,
            AgentEventKind.AssistantMessageCompleted,
            new AgentMessagePayload(messageEntryId, text));
    }

    private static AgentEvent CreateFailureReportedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentFailureKind failureKind,
        string reason,
        long sequence = 2)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendId.FromValue("backend:test"),
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideMediated,
            AgentEventKind.FailureReported,
            new AgentFailurePayload(failureKind, reason));
    }

    private static AgentEvent CreateRunCancelledEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        long sequence = 2)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendId.FromValue("backend:test"),
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideExecuted,
            AgentEventKind.RunCancelled,
            new AgentRunLifecyclePayload(AgentRunStatus.Cancelled));
    }

    private static AgentEvent CreateRunTimedOutEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        long sequence = 2)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendId.FromValue("backend:test"),
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideExecuted,
            AgentEventKind.RunTimedOut,
            new AgentRunLifecyclePayload(AgentRunStatus.TimedOut));
    }

    private static AgentEvent CreateRunRejectedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        long sequence = 1)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendId.FromValue("backend:test"),
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideExecuted,
            AgentEventKind.RunRejected,
            new AgentRunLifecyclePayload(AgentRunStatus.Rejected));
    }

    [Fact]
    public void UserMessageAdmitted_ProjectsSingleUserChatEntryWithCorrelationAndExactText()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var msgEntryId = ConversationEntryId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, conversation.Id, msgEntryId, "hello world"));

        Assert.Single(conversation.Entries);
        var entry = conversation.Entries[0];
        Assert.Equal(msgEntryId, entry.Id);
        Assert.Equal(ConversationEntryKind.UserChat, entry.Kind);
        Assert.Equal(ActorId.HumanUser, entry.Author);
        Assert.Equal("hello world", entry.Content);
        Assert.NotNull(entry.CorrelationId);
        Assert.Equal(runId.Value, entry.CorrelationId!.Value.Value);
    }

    [Fact]
    public void AssistantMessageCompleted_ProjectsSingleAssistantResponseEntryWithCorrelationAndExactText()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var userMsgId = ConversationEntryId.New();
        var assistantMsgId = ConversationEntryId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, conversation.Id, userMsgId, "hello world", sequence: 1));
        stream.Publish(CreateAssistantCompletedEvent(sessionId, runId, conversation.Id, assistantMsgId, "hello back", sequence: 2));

        Assert.Equal(2, conversation.Entries.Count);
        var assistantEntry = conversation.Entries[1];
        Assert.Equal(assistantMsgId, assistantEntry.Id);
        Assert.Equal(ConversationEntryKind.AssistantResponse, assistantEntry.Kind);
        Assert.Equal(agentActor, assistantEntry.Author);
        Assert.Equal("hello back", assistantEntry.Content);
        Assert.NotNull(assistantEntry.CorrelationId);
        Assert.Equal(runId.Value, assistantEntry.CorrelationId!.Value.Value);
    }

    [Fact]
    public void DuplicateEvents_AreIdempotentAndDoNotCreateDuplicateEntries()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var userMsgId = ConversationEntryId.New();
        var assistantMsgId = ConversationEntryId.New();

        var userEvent = CreateUserAdmittedEvent(sessionId, runId, conversation.Id, userMsgId, "hello", sequence: 1);
        var assistantEvent = CreateAssistantCompletedEvent(sessionId, runId, conversation.Id, assistantMsgId, "reply", sequence: 2);

        stream.Publish(userEvent);
        stream.Publish(userEvent); // duplicate
        stream.Publish(assistantEvent);
        stream.Publish(assistantEvent); // duplicate

        Assert.Equal(2, conversation.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);
    }

    [Fact]
    public void FailureReported_ProjectsSingleExecutionFailureEntryWithExactReason()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var userMsgId = ConversationEntryId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, conversation.Id, userMsgId, "fail me", sequence: 1));
        stream.Publish(CreateFailureReportedEvent(sessionId, runId, conversation.Id, AgentFailureKind.Execution, "Server error 500", sequence: 2));
        stream.Publish(CreateFailureReportedEvent(sessionId, runId, conversation.Id, AgentFailureKind.Execution, "Server error 500", sequence: 3)); // duplicate

        Assert.Equal(2, conversation.Entries.Count);
        var failureEntry = conversation.Entries[1];
        Assert.Equal(ConversationEntryKind.ExecutionFailure, failureEntry.Kind);
        Assert.Equal(agentActor, failureEntry.Author);
        Assert.Equal("Server error 500", failureEntry.Content);
        Assert.Equal(runId.Value, failureEntry.CorrelationId!.Value.Value);
    }

    [Fact]
    public void CancellationAndTimeout_ProjectExactExistingTownhallStrings()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();

        // Run 1: Cancelled
        var runId1 = ExecutionRunId.New();
        stream.Publish(CreateUserAdmittedEvent(sessionId, runId1, conversation.Id, ConversationEntryId.New(), "to cancel", sequence: 1));
        stream.Publish(CreateRunCancelledEvent(sessionId, runId1, conversation.Id, sequence: 2));

        // Run 2: Timed out
        var runId2 = ExecutionRunId.New();
        stream.Publish(CreateUserAdmittedEvent(sessionId, runId2, conversation.Id, ConversationEntryId.New(), "to timeout", sequence: 3));
        stream.Publish(CreateRunTimedOutEvent(sessionId, runId2, conversation.Id, sequence: 4));

        Assert.Equal(4, conversation.Entries.Count);
        Assert.Equal(ConversationEntryKind.ExecutionFailure, conversation.Entries[1].Kind);
        Assert.Equal("The operation was canceled.", conversation.Entries[1].Content);

        Assert.Equal(ConversationEntryKind.ExecutionFailure, conversation.Entries[3].Kind);
        Assert.Equal("Request timed out.", conversation.Entries[3].Content);
    }

    [Fact]
    public void Rejection_DoesNotCreateUserOrFailureEntryInConversation()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(CreateRunRejectedEvent(sessionId, runId, conversation.Id, sequence: 1));
        stream.Publish(CreateFailureReportedEvent(sessionId, runId, conversation.Id, AgentFailureKind.Execution, "An active run is already in progress", sequence: 2));

        Assert.Empty(conversation.Entries);
    }

    [Fact]
    public void PrivateConversationOwnership_DoesNotMirrorToPublicChannels()
    {
        var store = ConversationsTestSupport.CreateStore();
        var channelConv = store.CreateChannelConversation("townhall-main");
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var directConv = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, directConv.Id, ConversationEntryId.New(), "secret text", sequence: 1));
        stream.Publish(CreateAssistantCompletedEvent(sessionId, runId, directConv.Id, ConversationEntryId.New(), "secret reply", sequence: 2));

        Assert.Equal(2, directConv.Entries.Count);
        Assert.Empty(channelConv.Entries);
    }

    [Fact]
    public void ProjectRoutingFailure_AppendsSingleRoutingFailureEntry()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var runId = ExecutionRunId.New();

        AgentConversationEventProjection.ProjectRoutingFailure(
            store,
            conversation.Id,
            agentActor,
            runId,
            "Unknown target");

        // Duplicate call for idempotency
        AgentConversationEventProjection.ProjectRoutingFailure(
            store,
            conversation.Id,
            agentActor,
            runId,
            "Unknown target");

        Assert.Single(conversation.Entries);
        var entry = conversation.Entries[0];
        Assert.Equal(ConversationEntryKind.RoutingFailure, entry.Kind);
        Assert.Equal("Unknown target", entry.Content);
        Assert.Equal(runId.Value, entry.CorrelationId!.Value.Value);
    }

    [Fact]
    public void NoAutoResume_HistoricalEntriesAreNotConvertedToActiveRunOrReEmitted()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var runId = ExecutionRunId.New();

        // Simulate historical entry
        store.AppendEntry(
            conversation.Id,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "historical user message",
                ExecutionRunCorrelation.ToEntryCorrelation(runId)));

        store.AppendEntry(
            conversation.Id,
            ConversationEntry.AssistantResponse(
                ConversationEntryId.New(),
                agentActor,
                DateTimeOffset.UtcNow,
                "historical assistant message",
                ExecutionRunCorrelation.ToEntryCorrelation(runId)));

        // Create new stream and projection over existing store
        var stream = new AgentEventStream();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        // Verification: Entries count remains exactly 2, no extra runs or auto-resumed work
        Assert.Equal(2, conversation.Entries.Count);
    }
}
