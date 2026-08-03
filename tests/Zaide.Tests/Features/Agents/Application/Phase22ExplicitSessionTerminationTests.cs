using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents.Acp.Backend;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 22.3 M2 explicit live-session termination: Townhall command/control,
/// ordered projection, bounded acknowledgement, ownership removal, and truthfulness.
/// Uses TaskCompletionSource gates; no timing sleeps.
/// </summary>
public sealed class Phase22ExplicitSessionTerminationTests
{
    private static readonly AgentBackendId TestBackendId = AgentBackendId.FromValue("backend:test");

    private static AgentEvent CreateEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        long sequence,
        AgentEventKind kind,
        AgentEventPayload payload) =>
        new(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            TestBackendId,
            sequence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideExecuted,
            kind,
            payload);

    private static (
        TownhallViewModel ViewModel,
        IConversationStore Store,
        AgentPanelHost Host,
        IAgentExecutionCoordinator Coordinator,
        IActorCatalog Catalog,
        FakeAgentBackend Backend,
        IAgentSessionService Session) CreateSurface(
        Action<FakeAgentBackend>? configureBackend = null)
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            store,
            draftState,
            catalog: catalog);
        if (configureBackend is null)
        {
            backend.SetCompletion("ok");
        }
        else
        {
            configureBackend(backend);
        }

        // Replace empty-catalog projection from helper with catalog-aware writer.
        _ = new AgentConversationEventProjection(session.Events, store, catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var state = new TownhallState();
        var uiState = new TownhallConversationUiState(draftState);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            state: state,
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: uiState,
            draftState: draftState,
            agentRouter: router,
            sessionService: session);

        return (vm, store, host, coordinator, catalog, backend, session);
    }

    private static async Task WaitForExecutionStartedAsync(FakeAgentBackend backend) =>
        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    private static async Task WaitForLiveSessionAsync(
        IAgentSessionService session,
        ConversationId conversationId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (session.TryGetSessionSnapshot(conversationId) is not null
                && session.TryGetActiveRunSnapshot(conversationId) is { } run
                && run.Status is AgentRunStatus.Running or AgentRunStatus.Accepted)
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("Live session did not become active.");
    }

    private static IEnumerable<ConversationEntry> SystemEntries(
        Conversation conversation,
        string prefix) =>
        conversation.Entries.Where(e =>
            e.Kind == ConversationEntryKind.SystemNotification
            && e.Content.StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public void Townhall_EndSessionCommandAndControl_AreReachableForDirectConversation()
    {
        var (vm, _, _, _, _, _, _) = CreateSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();

        Assert.NotNull(vm.EndSessionCommand);
        Assert.True(vm.CanEndSession);

        var panel = new AgentBackendBindingPanel();
        panel.SetWorkflowProjection(
            "Native Harness",
            "bound",
            isDisconnected: false,
            capabilityCaption: string.Empty,
            settingsCaption: string.Empty,
            mutationErrorCaption: null,
            canBindNativeHarness: false,
            canUnbind: true,
            canEndSession: true);

        Assert.True(panel.EndSessionButton.IsVisible);
        Assert.True(panel.EndSessionButton.IsEnabled);
        Assert.Equal(
            "End agent session",
            Avalonia.Automation.AutomationProperties.GetName(panel.EndSessionButton));
    }

    [Fact]
    public void Townhall_ChannelConversation_CannotEndSession()
    {
        var (vm, _, _, _, _, _, _) = CreateSurface();
        var channel = vm.Channels.First();
        vm.SelectChannelCommand.Execute(channel.Id).Subscribe();

        Assert.False(vm.CanEndSession);
    }

    [Fact]
    public async Task EndSession_OperatesOnCapturedDirectConversation_NavigationDoesNotRedirect()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (vm, store, _, _, _, backend, session) = CreateSurface(b =>
            b.SetGatedCompletion(gate, "late"));

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceId = vm.ActiveConversationId!.Value;
        Assert.True(store.TryGet(sourceId, out var sourceConversation));
        var historyBefore = sourceConversation!.Entries.Count;

        vm.DraftText = "in flight";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(backend);
        await WaitForLiveSessionAsync(session, sourceId);

        var endTask = vm.EndSessionCommand.Execute().ToTask();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("beta")).Subscribe();
        Assert.NotEqual(sourceId, vm.ActiveConversationId);

        await endTask;
        gate.TrySetResult("ignored-after-cancel");
        try
        {
            await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Send may complete cancelled; End owns terminal truth.
        }

        Assert.Null(session.TryGetSessionSnapshot(sourceId));
        Assert.Null(session.TryGetSessionSnapshot(vm.ActiveConversationId!.Value));
        Assert.True(store.TryGet(sourceId, out var after));
        Assert.True(after!.Entries.Count >= historyBefore);
        Assert.Contains(
            after.Entries,
            e => e.Kind == ConversationEntryKind.SystemNotification
                 && e.Content.StartsWith(
                     AgentConversationEventProjection.SessionEndedContentPrefix,
                     StringComparison.Ordinal));
        Assert.DoesNotContain(
            after.Entries,
            e => e.Content.Contains("deleted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EndAsync_ProjectsOrderedIntentEndingEnded_ExactlyOnce_AndRemovesOwnership()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (vm, store, _, _, _, backend, session) = CreateSurface(b =>
            b.SetGatedCompletion(gate, "done"));

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "terminate me";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(backend);
        await WaitForLiveSessionAsync(session, conversationId);
        var firstSessionId = session.TryGetSessionSnapshot(conversationId)!.SessionId;

        await vm.EndSessionCommand.Execute().ToTask();
        await vm.EndSessionCommand.Execute().ToTask();
        try
        {
            await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }

        Assert.Null(session.TryGetSessionSnapshot(conversationId));
        Assert.True(store.TryGet(conversationId, out var conversation));
        var entries = conversation!.Entries.ToList();

        var ending = SystemEntries(conversation, AgentConversationEventProjection.SessionEndingContentPrefix).ToList();
        var ended = SystemEntries(conversation, AgentConversationEventProjection.SessionEndedContentPrefix).ToList();
        var intent = SystemEntries(conversation, AgentConversationEventProjection.CancellationIntentContentPrefix).ToList();

        Assert.Single(ending);
        Assert.Single(ended);
        Assert.Single(intent);

        var endingIndex = entries.IndexOf(ending[0]);
        var endedIndex = entries.IndexOf(ended[0]);
        Assert.True(endingIndex < endedIndex);
        Assert.Contains("Provider termination is not claimed", ended[0].Content, StringComparison.Ordinal);

        // Fresh subsequent send creates a new session without resume.
        backend.SetCompletion("fresh");
        vm.DraftText = "after end";
        await vm.SendMessageCommand.Execute().ToTask();
        var secondSession = session.TryGetSessionSnapshot(conversationId);
        Assert.NotNull(secondSession);
        Assert.NotEqual(firstSessionId, secondSession!.SessionId);
        Assert.Contains(conversation.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "terminate me");
        Assert.Contains(conversation.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "after end");
    }

    [Fact]
    public async Task BoundedNativeHarnessAcknowledgement_TimeoutIsIndeterminateAndRetryable()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousTimeout = AgentSessionService.EndAcknowledgementTimeout;
        AgentSessionService.EndAcknowledgementTimeout = TimeSpan.FromMilliseconds(50);
        try
        {
            var (vm, store, _, _, _, backend, session) = CreateSurface(b =>
                b.SetGatedCompletionIgnoringCancellation(gate, "eventually"));

            vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
            var conversationId = vm.ActiveConversationId!.Value;
            vm.DraftText = "hold";
            _ = vm.SendMessageCommand.Execute().ToTask();
            await WaitForExecutionStartedAsync(backend);
            await WaitForLiveSessionAsync(session, conversationId);

            await vm.EndSessionCommand.Execute().ToTask();

            Assert.NotNull(session.TryGetSessionSnapshot(conversationId));
            Assert.Equal(
                AgentSessionStatus.Ending,
                session.TryGetSessionSnapshot(conversationId)!.Status);
            Assert.True(store.TryGet(conversationId, out var conversation));
            var indeterminate = SystemEntries(
                    conversation!,
                    AgentConversationEventProjection.TerminationIndeterminateContentPrefix)
                .ToList();
            Assert.Single(indeterminate);
            Assert.Contains("Retry", indeterminate[0].Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "Provider termination is not claimed",
                indeterminate[0].Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                indeterminate[0].Content,
                "deleted",
                StringComparison.OrdinalIgnoreCase);

            // Retry after backend finally completes.
            gate.TrySetResult("eventually");
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var active = session.TryGetActiveRunSnapshot(conversationId);
                if (active is null || active.Status is AgentRunStatus.Completed
                        or AgentRunStatus.Cancelled
                        or AgentRunStatus.Failed
                        or AgentRunStatus.Indeterminate)
                {
                    break;
                }

                await Task.Yield();
            }

            await vm.EndSessionCommand.Execute().ToTask();
            Assert.Null(session.TryGetSessionSnapshot(conversationId));
            Assert.True(store.TryGet(conversationId, out var endedConversation));
            Assert.Contains(
                endedConversation!.Entries,
                e => e.Content.StartsWith(
                    AgentConversationEventProjection.SessionEndedContentPrefix,
                    StringComparison.Ordinal));
        }
        finally
        {
            AgentSessionService.EndAcknowledgementTimeout = previousTimeout;
            gate.TrySetResult("cleanup");
        }
    }

    [Fact]
    public void LateCompletionAfterTerminationIntent_IsRetainedAndLabelled()
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

        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 1,
            AgentEventKind.UserMessageAdmitted,
            new AgentMessagePayload(userEntryId, "race")));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 2,
            AgentEventKind.SessionEnding,
            new AgentSessionLifecyclePayload(AgentSessionStatus.Ending)));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 3,
            AgentEventKind.RunCancellationRequested,
            new AgentRunLifecyclePayload(AgentRunStatus.CancellationRequested)));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 4,
            AgentEventKind.AssistantMessageCompleted,
            new AgentMessagePayload(assistantEntryId, "late winner")));

        Assert.Contains(
            conversation.Entries,
            e => e.Kind == ConversationEntryKind.SystemNotification
                 && e.Content.StartsWith(
                     AgentConversationEventProjection.CancellationIntentContentPrefix,
                     StringComparison.Ordinal));
        Assert.Contains(
            conversation.Entries,
            e => e.Kind == ConversationEntryKind.AssistantResponse
                 && e.Content == "late winner");
        Assert.Contains(
            conversation.Entries,
            e => e.Kind == ConversationEntryKind.SystemNotification
                 && e.Content.StartsWith(
                     AgentConversationEventProjection.LateCompletionLabelPrefix,
                     StringComparison.Ordinal)
                 && e.Content.Contains("late winner", StringComparison.Ordinal));
        Assert.Contains(
            conversation.Entries,
            e => e.Kind == ConversationEntryKind.SystemNotification
                 && e.Content.StartsWith(
                     AgentConversationEventProjection.SessionEndingContentPrefix,
                     StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcpCancellation_UsesIndependentBoundedToken_NotAlreadyCancelledRunToken()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AcpFakeSessionClient(new AcpFakeSessionScript())
        {
            PromptHoldAsync = async ct =>
            {
                holdEntered.TrySetResult();
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
                await tcs.Task.ConfigureAwait(false);
            },
        };

        var backend = new AcpAgentBackend(
            new DelegatingAcpSessionClientFactory(_ => Task.FromResult<IAcpSessionClient>(client)),
            () => "/tmp/zaide-acp-m2");
        var context = new AgentBackendExecutionContext(
            new AgentBackendRequest(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.HumanUser,
                ActorId.PanelSeed("alpha"),
                ConversationEntryId.New(),
                "cancel path"),
            new UnavailableAgentActionBroker());

        using var runCts = new CancellationTokenSource();
        var events = new List<AgentBackendEvent>();
        var executeTask = Task.Run(async () =>
        {
            await foreach (var ev in backend.ExecuteAsync(context, runCts.Token))
            {
                events.Add(ev);
            }
        });

        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runCts.Cancel();
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, client.CancelPromptCallCount);
        Assert.False(client.LastCancelTokenWasCancellationRequested);
        Assert.Equal(client.ActiveSessionId, client.LastCancelSessionId);
        Assert.Contains(
            events,
            e => e.Kind == AgentBackendEventKind.FailureObserved
                 && e.Payload is AgentBackendFailurePayload failure
                 && failure.FailureKind == AgentFailureKind.Cancellation);
    }

    [Fact]
    public async Task EndAsync_NoLiveSession_ReturnsNoLiveSessionWithoutProviderClaims()
    {
        var session = new AgentSessionService(
            new[] { new FakeAgentBackend(AgentBackendId.FromValue("backend:fake")) },
            new AgentEventStream());
        var conversationId = ConversationId.NewDirect();

        var result = await session.EndAsync(conversationId);

        Assert.Equal(AgentSessionEndStatus.NoLiveSession, result.Status);
        Assert.Null(session.TryGetSessionSnapshot(conversationId));
    }

    [Fact]
    public void Projection_SessionEndingAndEnded_AreIdempotentPerSession()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, ActorId.PanelSeed("alpha"));
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 1,
            AgentEventKind.UserMessageAdmitted,
            new AgentMessagePayload(ConversationEntryId.New(), "u")));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 2,
            AgentEventKind.SessionEnding,
            new AgentSessionLifecyclePayload(AgentSessionStatus.Ending)));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 3,
            AgentEventKind.SessionEnding,
            new AgentSessionLifecyclePayload(AgentSessionStatus.Ending)));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 4,
            AgentEventKind.SessionEnded,
            new AgentSessionLifecyclePayload(AgentSessionStatus.Ended)));
        stream.Publish(CreateEvent(
            sessionId,
            runId,
            conversation.Id,
            sequence: 5,
            AgentEventKind.SessionEnded,
            new AgentSessionLifecyclePayload(AgentSessionStatus.Ended)));

        Assert.Single(SystemEntries(conversation, AgentConversationEventProjection.SessionEndingContentPrefix));
        Assert.Single(SystemEntries(conversation, AgentConversationEventProjection.SessionEndedContentPrefix));
        Assert.DoesNotContain(
            conversation.Entries,
            e => e.Content.Contains("provider deleted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TownhallEntryProjection_FormatsTerminationPrefixesWithoutRawProtocolNoise()
    {
        var ending = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.PanelSeed("alpha"),
            DateTimeOffset.UtcNow,
            AgentConversationEventProjection.FormatSessionEndingContent());
        var ended = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.PanelSeed("alpha"),
            DateTimeOffset.UtcNow,
            AgentConversationEventProjection.FormatSessionEndedContent());
        var indeterminate = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.PanelSeed("alpha"),
            DateTimeOffset.UtcNow,
            AgentConversationEventProjection.FormatTerminationIndeterminateContent(
                "Backend acknowledgement timed out. Retry is available. Provider termination is not claimed."));
        var late = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.PanelSeed("alpha"),
            DateTimeOffset.UtcNow,
            AgentConversationEventProjection.FormatLateCompletionContent("body"));

        Assert.Equal("Session ending.", TownhallEntryProjection.ToTownhallDisplayContent(ending));
        Assert.Contains(
            "Provider termination is not claimed",
            TownhallEntryProjection.ToTownhallDisplayContent(ended),
            StringComparison.Ordinal);
        Assert.Contains(
            "Retry",
            TownhallEntryProjection.ToTownhallDisplayContent(indeterminate),
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            "Late completion after cancellation:",
            TownhallEntryProjection.ToTownhallDisplayContent(late),
            StringComparison.Ordinal);
    }
}
