using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 22.3 M1 outcome projection tests.
/// </summary>
public sealed class Phase22AgentOutcomeProjectionTests
{
    private static AgentEvent CreateUserAdmittedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ConversationEntryId messageEntryId,
        string text,
        long sequence = 1) =>
        new(
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

    private static AgentEvent CreateAssistantCompletedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ConversationEntryId messageEntryId,
        string text,
        long sequence = 2) =>
        new(
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

    private static AgentEvent CreateFailureReportedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentFailureKind failureKind,
        string reason,
        long sequence = 2) =>
        new(
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

    private static AgentEvent CreateRunLifecycleEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentEventKind kind,
        long sequence)
    {
        var status = kind switch
        {
            AgentEventKind.RunAccepted => AgentRunStatus.Accepted,
            AgentEventKind.RunRunning => AgentRunStatus.Running,
            AgentEventKind.RunFailed => AgentRunStatus.Failed,
            AgentEventKind.RunCancelled => AgentRunStatus.Cancelled,
            AgentEventKind.RunTimedOut => AgentRunStatus.TimedOut,
            AgentEventKind.RunDisconnected => AgentRunStatus.Disconnected,
            AgentEventKind.RunIndeterminate => AgentRunStatus.Indeterminate,
            AgentEventKind.RunCancellationRequested => AgentRunStatus.CancellationRequested,
            AgentEventKind.RunCompleted => AgentRunStatus.Completed,
            _ => AgentRunStatus.Failed,
        };

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
            kind,
            new AgentRunLifecyclePayload(status));
    }

    [Fact]
    public async Task DirectUnboundRejection_ProducesExactlyOneCorrelatedExecutionFailure()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var bindingStore = new AgentActorBackendBindingStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var panel = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var backend = new FakeAgentBackend(AgentBackendId.FromValue("backend:test"));
        var session = new AgentSessionService(new[] { backend }, new AgentEventStream());
        _ = new AgentConversationEventProjection(session.Events, store, catalog);
        var coordinator = new AgentExecutionCoordinator(host, session, store, bindingStore);

        var result = await coordinator.SendAsync(panel.PanelId, "hello");

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.Rejected, result!.Run.Outcome);
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Single(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.ExecutionFailure
                 && e.CorrelationId!.Value.Value == result.Run.Id.Value);
    }

    [Fact]
    public async Task SessionAdmissionRejection_ProducesExactlyOneExecutionFailureWithoutDuplicateWriters()
    {
        var (host, panel, store, backend, session) = CreateAdmissionSurface();
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "busy");
        var coordinator = AgentExecutionTestSupport.CreateCoordinator(host, session, store, backend.BackendId);
        _ = new AgentConversationEventProjection(session.Events, store, ConversationsTestSupport.CreateCatalog());

        _ = coordinator.SendAsync(panel.PanelId, "first");
        await WaitForRunningAsync(session, panel.ConversationId);
        var rejected = await coordinator.SendAsync(panel.PanelId, "second");

        Assert.Equal(ExecutionRunOutcome.Rejected, rejected!.Run.Outcome);
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        var failures = conversation!.Entries
            .Where(e => e.Kind == ConversationEntryKind.ExecutionFailure
                        && e.CorrelationId!.Value.Value == rejected.Run.Id.Value)
            .ToList();
        Assert.Single(failures);
        Assert.Single(conversation.Entries, e => e.Kind == ConversationEntryKind.UserChat);
    }

    [Fact]
    public void AdmittedRunFailed_ProjectsExactFailureLabel()
        => AssertTerminalLabel(AgentEventKind.RunFailed, "Request failed.");

    [Fact]
    public void AdmittedRunCancelled_ProjectsExactFailureLabel()
        => AssertTerminalLabel(AgentEventKind.RunCancelled, "The operation was canceled.");

    [Fact]
    public void AdmittedRunTimedOut_ProjectsExactFailureLabel()
        => AssertTerminalLabel(AgentEventKind.RunTimedOut, "Request timed out.");

    [Fact]
    public void AdmittedRunDisconnected_ProjectsExactFailureLabel()
        => AssertTerminalLabel(AgentEventKind.RunDisconnected, "Connection was lost.");

    [Fact]
    public void AdmittedRunIndeterminate_ProjectsExactFailureLabel()
        => AssertTerminalLabel(AgentEventKind.RunIndeterminate, "Request ended indeterminately.");

    private static void AssertTerminalLabel(AgentEventKind terminalKind, string expectedLabel)
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, conversation.Id, ConversationEntryId.New(), "go", sequence: 1));
        stream.Publish(CreateRunLifecycleEvent(
            sessionId,
            runId,
            conversation.Id,
            terminalKind,
            sequence: 2));

        var failure = Assert.Single(
            conversation.Entries,
            e => e.Kind == ConversationEntryKind.ExecutionFailure);
        Assert.Equal(expectedLabel, failure.Content);
    }

    [Fact]
    public void AcceptedAndRunning_DoNotCreateConversationEntries()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, conversation.Id, ConversationEntryId.New(), "go", sequence: 1));
        stream.Publish(CreateRunLifecycleEvent(sessionId, runId, conversation.Id, AgentEventKind.RunAccepted, sequence: 2));
        stream.Publish(CreateRunLifecycleEvent(sessionId, runId, conversation.Id, AgentEventKind.RunRunning, sequence: 3));

        Assert.Single(conversation.Entries, e => e.Kind == ConversationEntryKind.UserChat);
        Assert.DoesNotContain(
            conversation.Entries,
            e => e.Content.Contains("Queued", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void QueuedRunStatus_IsUnsupportedAndNotProjected()
    {
        Assert.DoesNotContain(
            Enum.GetNames(typeof(AgentRunStatus)),
            name => string.Equals(name, "Queued", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CancellationIntentThenLateCompletion_PreservesOrderedFactsWithoutErasingIntent()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var userEntryId = ConversationEntryId.New();
        var assistantEntryId = ConversationEntryId.New();

        stream.Publish(CreateUserAdmittedEvent(sessionId, runId, conversation.Id, userEntryId, "race", sequence: 1));
        stream.Publish(CreateRunLifecycleEvent(
            sessionId,
            runId,
            conversation.Id,
            AgentEventKind.RunCancellationRequested,
            sequence: 2));
        stream.Publish(CreateAssistantCompletedEvent(
            sessionId,
            runId,
            conversation.Id,
            assistantEntryId,
            "late winner",
            sequence: 3));
        stream.Publish(CreateRunLifecycleEvent(
            sessionId,
            runId,
            conversation.Id,
            AgentEventKind.RunCompleted,
            sequence: 4));

        Assert.Equal(3, conversation.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal(ConversationEntryKind.SystemNotification, conversation.Entries[1].Kind);
        Assert.Contains("Cancellation requested", conversation.Entries[1].Content, StringComparison.Ordinal);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[2].Kind);
        Assert.Equal("late winner", conversation.Entries[2].Content);
        Assert.DoesNotContain(
            conversation.Entries,
            e => e.Kind == ConversationEntryKind.ExecutionFailure);
    }

    [Fact]
    public void ProjectAdmissionRejection_IsIdempotentForOneRunCorrelation()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        var runId = ExecutionRunId.New();

        AgentConversationEventProjection.ProjectAdmissionRejection(
            store,
            conversation.Id,
            agentActor,
            runId,
            "No explicit backend binding exists for this actor.");
        AgentConversationEventProjection.ProjectAdmissionRejection(
            store,
            conversation.Id,
            agentActor,
            runId,
            "No explicit backend binding exists for this actor.");

        Assert.Single(conversation.Entries, e => e.Kind == ConversationEntryKind.ExecutionFailure);
    }

    private static (AgentPanelHost Host, AgentPanelState Panel, ConversationStore Store, FakeAgentBackend Backend, IAgentSessionService Session)
        CreateAdmissionSurface()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        _ = coordinator;
        return (host, panel, store, backend, session);
    }

    private static async Task WaitForRunningAsync(IAgentSessionService session, ConversationId conversationId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (session.TryGetActiveRunSnapshot(conversationId)?.Status == AgentRunStatus.Running)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new InvalidOperationException("Timed out waiting for running session.");
    }
}
