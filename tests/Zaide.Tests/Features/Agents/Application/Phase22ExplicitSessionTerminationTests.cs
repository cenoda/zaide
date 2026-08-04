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
/// Uses TaskCompletionSource gates; no timing sleeps. Surfaces are disposed and
/// in-flight command/gate tasks are observed before teardown for parallel stability.
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

    /// <summary>
    /// Owns every disposable M2 Townhall/session/projection surface for one test.
    /// </summary>
    private sealed class SurfaceHarness : IDisposable
    {
        public required TownhallViewModel ViewModel { get; init; }
        public required IConversationStore Store { get; init; }
        public required AgentPanelHost Host { get; init; }
        public required IAgentExecutionCoordinator Coordinator { get; init; }
        public required IActorCatalog Catalog { get; init; }
        public required FakeAgentBackend Backend { get; init; }
        public required AgentSessionService Session { get; init; }
        public required AgentConversationEventProjection Projection { get; init; }

        public void Dispose()
        {
            ViewModel.Dispose();
            Projection.Dispose();
            Session.Dispose();
        }
    }

    private static SurfaceHarness CreateSurface(Action<FakeAgentBackend>? configureBackend = null)
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, backend, sessionService) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
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

        // Catalog-aware writer (coordinator helper also attaches a projection; both are
        // event subscribers only — dispose the catalog-aware one we own).
        var projection = new AgentConversationEventProjection(sessionService.Events, store, catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var state = new TownhallState();
        var uiState = new TownhallConversationUiState(draftState);
        var session = (AgentSessionService)sessionService;
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

        return new SurfaceHarness
        {
            ViewModel = vm,
            Store = store,
            Host = host,
            Coordinator = coordinator,
            Catalog = catalog,
            Backend = backend,
            Session = session,
            Projection = projection,
        };
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

    private static async Task WaitForCanEndSessionAsync(TownhallViewModel vm, bool expected)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (vm.CanEndSession == expected)
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException($"CanEndSession did not become {expected}.");
    }

    private static async Task OpenDirectAsync(TownhallViewModel vm, ActorId agent) =>
        await vm.OpenDirectConversationCommand.Execute(agent).ToTask();

    private static async Task SelectChannelAsync(TownhallViewModel vm, string channelId) =>
        await vm.SelectChannelCommand.Execute(channelId).ToTask();

    private static async Task SelectConversationAsync(TownhallViewModel vm, ConversationId id) =>
        await vm.SelectConversationCommand.Execute(id).ToTask();

    private static async Task ObserveOptionalAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Cancelled/faulted send or end after ownership change is acceptable.
        }
    }

    private static IEnumerable<ConversationEntry> SystemEntries(
        Conversation conversation,
        string prefix) =>
        conversation.Entries.Where(e =>
            e.Kind == ConversationEntryKind.SystemNotification
            && e.Content.StartsWith(prefix, StringComparison.Ordinal));

    private static async Task WaitForActiveRunAsync(
        IAgentSessionService sessionService,
        ConversationId conversationId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (sessionService.TryGetSessionSnapshot(conversationId) is not null
                && sessionService.TryGetActiveRunSnapshot(conversationId) is { } run
                && run.Status is AgentRunStatus.Running or AgentRunStatus.Accepted)
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("ACP live run did not become active.");
    }

    private sealed class AcpEndHarness : IDisposable
    {
        public required AcpFakeSessionClient Client { get; init; }
        public required AcpAgentBackend Backend { get; init; }
        public required AgentSessionService SessionService { get; init; }
        public required IConversationStore Store { get; init; }
        public required IActorCatalog Catalog { get; init; }
        public required Conversation Conversation { get; init; }
        public required ActorId AgentActor { get; init; }
        public required AgentConversationEventProjection Projection { get; init; }

        public void Dispose()
        {
            Projection.Dispose();
            SessionService.Dispose();
        }
    }

    private static AcpEndHarness CreateAcpEndSurface(
        Func<CancellationToken, Task>? promptHoldAsync,
        Func<string, CancellationToken, Task>? cancelOverride,
        TimeSpan? endAcknowledgementTimeout = null)
    {
        var client = new AcpFakeSessionClient(new AcpFakeSessionScript())
        {
            PromptHoldAsync = promptHoldAsync,
            CancelPromptAsyncOverride = cancelOverride,
        };
        var backend = new AcpAgentBackend(
            new DelegatingAcpSessionClientFactory(_ => Task.FromResult<IAcpSessionClient>(client)),
            () => "/tmp/zaide-acp-m2-retry");
        var sessionService = new AgentSessionService(new[] { backend }, new AgentEventStream());
        if (endAcknowledgementTimeout is { } timeout)
        {
            sessionService.EndAcknowledgementTimeout = timeout;
        }

        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var projection = new AgentConversationEventProjection(sessionService.Events, store, catalog);
        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);
        return new AcpEndHarness
        {
            Client = client,
            Backend = backend,
            SessionService = sessionService,
            Store = store,
            Catalog = catalog,
            Conversation = conversation,
            AgentActor = agentActor,
            Projection = projection,
        };
    }

    private static Func<CancellationToken, Task> HoldUntilCancelled(
        TaskCompletionSource holdEntered) =>
        async ct =>
        {
            holdEntered.TrySetResult();
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            await tcs.Task.ConfigureAwait(false);
        };

    [Fact]
    public async Task DirectConversation_WithoutLiveOwnership_HidesEndSession()
    {
        using var surface = CreateSurface();
        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));

        Assert.NotNull(surface.ViewModel.EndSessionCommand);
        Assert.False(surface.ViewModel.CanEndSession);

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
            canEndSession: false);

        Assert.False(panel.EndSessionButton.IsVisible);
        Assert.False(panel.EndSessionButton.IsEnabled);
    }

    [Fact]
    public async Task Townhall_ChannelConversation_CannotEndSession()
    {
        using var surface = CreateSurface();
        var channel = surface.ViewModel.Channels.First();
        await SelectChannelAsync(surface.ViewModel, channel.Id);

        Assert.False(surface.ViewModel.CanEndSession);
    }

    [Fact]
    public async Task AdmittedLiveSession_EnablesEndSession()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b => b.SetGatedCompletion(gate, "live"));

        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
        Assert.False(surface.ViewModel.CanEndSession);

        var conversationId = surface.ViewModel.ActiveConversationId!.Value;
        surface.ViewModel.DraftText = "admit";
        var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(surface.Backend);
        await WaitForLiveSessionAsync(surface.Session, conversationId);
        await WaitForCanEndSessionAsync(surface.ViewModel, expected: true);

        Assert.True(surface.ViewModel.CanEndSession);
        Assert.NotNull(surface.Session.TryGetSessionSnapshot(conversationId));

        gate.TrySetResult("live");
        await ObserveOptionalAsync(sendTask);
    }

    [Fact]
    public async Task SuccessfulTermination_DisablesEndSession()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b => b.SetGatedCompletion(gate, "done"));

        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
        var conversationId = surface.ViewModel.ActiveConversationId!.Value;
        surface.ViewModel.DraftText = "end me";
        var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(surface.Backend);
        await WaitForLiveSessionAsync(surface.Session, conversationId);
        await WaitForCanEndSessionAsync(surface.ViewModel, expected: true);

        await surface.ViewModel.EndSessionCommand.Execute().ToTask();
        await ObserveOptionalAsync(sendTask);

        Assert.Null(surface.Session.TryGetSessionSnapshot(conversationId));
        await WaitForCanEndSessionAsync(surface.ViewModel, expected: false);
        Assert.False(surface.ViewModel.CanEndSession);
    }

    [Fact]
    public async Task IndeterminateEndingOwnership_KeepsEndSessionRetryable()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b =>
            b.SetGatedCompletionIgnoringCancellation(gate, "eventually"));
        surface.Session.EndAcknowledgementTimeout = TimeSpan.FromMilliseconds(50);

        try
        {
            await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
            var conversationId = surface.ViewModel.ActiveConversationId!.Value;
            surface.ViewModel.DraftText = "hold";
            var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
            await WaitForExecutionStartedAsync(surface.Backend);
            await WaitForLiveSessionAsync(surface.Session, conversationId);

            await surface.ViewModel.EndSessionCommand.Execute().ToTask();

            Assert.NotNull(surface.Session.TryGetSessionSnapshot(conversationId));
            Assert.Equal(
                AgentSessionStatus.Ending,
                surface.Session.TryGetSessionSnapshot(conversationId)!.Status);
            await WaitForCanEndSessionAsync(surface.ViewModel, expected: true);
            Assert.True(surface.ViewModel.CanEndSession);
            Assert.True(surface.Store.TryGet(conversationId, out var conversation));
            Assert.Single(
                SystemEntries(
                    conversation!,
                    AgentConversationEventProjection.TerminationIndeterminateContentPrefix));

            gate.TrySetResult("cleanup");
            await ObserveOptionalAsync(sendTask);
        }
        finally
        {
            gate.TrySetResult("cleanup");
        }
    }

    [Fact]
    public async Task EndSession_OperatesOnCapturedDirectConversation_NavigationDoesNotRedirect()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b => b.SetGatedCompletion(gate, "late"));

        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
        var sourceId = surface.ViewModel.ActiveConversationId!.Value;
        Assert.True(surface.Store.TryGet(sourceId, out var sourceConversation));
        var historyBefore = sourceConversation!.Entries.Count;

        surface.ViewModel.DraftText = "in flight";
        var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(surface.Backend);
        await WaitForLiveSessionAsync(surface.Session, sourceId);

        var endTask = surface.ViewModel.EndSessionCommand.Execute().ToTask();
        // Deterministic await (not bare Subscribe) so ReactiveCommand errors surface here
        // and navigation completes before teardown.
        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("beta"));
        Assert.NotEqual(sourceId, surface.ViewModel.ActiveConversationId);

        await endTask;
        gate.TrySetResult("ignored-after-cancel");
        await ObserveOptionalAsync(sendTask);

        Assert.Null(surface.Session.TryGetSessionSnapshot(sourceId));
        Assert.Null(surface.Session.TryGetSessionSnapshot(surface.ViewModel.ActiveConversationId!.Value));
        Assert.True(surface.Store.TryGet(sourceId, out var after));
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
        Assert.True(surface.Store.TryGet(surface.ViewModel.ActiveConversationId!.Value, out var navigated));
        Assert.DoesNotContain(
            navigated!.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal)
                 || e.Content.StartsWith(
                     AgentConversationEventProjection.TerminationIndeterminateContentPrefix,
                     StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentNavigationDuringTermination_DoesNotThrowReactiveUnhandled()
    {
        // Regression for parallel-suite UnhandledErrorException / NRE in
        // ApplyUnreadPresentation when DirectNavItems is refreshed while entries append.
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b => b.SetGatedCompletion(gate, "late"));

        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
        var sourceId = surface.ViewModel.ActiveConversationId!.Value;
        surface.ViewModel.DraftText = "race";
        var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(surface.Backend);
        await WaitForLiveSessionAsync(surface.Session, sourceId);

        var endTask = surface.ViewModel.EndSessionCommand.Execute().ToTask();
        // Overlap navigation with termination projection without bare Subscribe.
        var navigateTask = OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("beta"));
        await Task.WhenAll(endTask, navigateTask);

        gate.TrySetResult("done");
        await ObserveOptionalAsync(sendTask);

        Assert.Null(surface.Session.TryGetSessionSnapshot(sourceId));
        Assert.NotNull(surface.ViewModel.DirectNavItems);
        Assert.True(surface.ViewModel.DirectNavItems.Count >= 1);
    }

    [Fact]
    public async Task EndAsync_ProjectsOrderedIntentEndingEnded_ExactlyOnce_AndRemovesOwnership()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b => b.SetGatedCompletion(gate, "done"));

        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
        var conversationId = surface.ViewModel.ActiveConversationId!.Value;

        surface.ViewModel.DraftText = "terminate me";
        var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(surface.Backend);
        await WaitForLiveSessionAsync(surface.Session, conversationId);
        var firstSessionId = surface.Session.TryGetSessionSnapshot(conversationId)!.SessionId;

        await surface.ViewModel.EndSessionCommand.Execute().ToTask();
        await surface.ViewModel.EndSessionCommand.Execute().ToTask();
        await ObserveOptionalAsync(sendTask);

        Assert.Null(surface.Session.TryGetSessionSnapshot(conversationId));
        Assert.True(surface.Store.TryGet(conversationId, out var conversation));
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

        surface.Backend.SetCompletion("fresh");
        surface.ViewModel.DraftText = "after end";
        await surface.ViewModel.SendMessageCommand.Execute().ToTask();
        var secondSession = surface.Session.TryGetSessionSnapshot(conversationId);
        Assert.NotNull(secondSession);
        Assert.NotEqual(firstSessionId, secondSession!.SessionId);
        Assert.Contains(conversation.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "terminate me");
        Assert.Contains(conversation.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "after end");
    }

    [Fact]
    public async Task BoundedNativeHarnessAcknowledgement_TimeoutIsIndeterminateAndRetryable()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var surface = CreateSurface(b =>
            b.SetGatedCompletionIgnoringCancellation(gate, "eventually"));
        surface.Session.EndAcknowledgementTimeout = TimeSpan.FromMilliseconds(50);

        try
        {
            await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));
            var conversationId = surface.ViewModel.ActiveConversationId!.Value;
            surface.ViewModel.DraftText = "hold";
            var sendTask = surface.ViewModel.SendMessageCommand.Execute().ToTask();
            await WaitForExecutionStartedAsync(surface.Backend);
            await WaitForLiveSessionAsync(surface.Session, conversationId);

            await surface.ViewModel.EndSessionCommand.Execute().ToTask();

            Assert.NotNull(surface.Session.TryGetSessionSnapshot(conversationId));
            Assert.Equal(
                AgentSessionStatus.Ending,
                surface.Session.TryGetSessionSnapshot(conversationId)!.Status);
            Assert.True(surface.Store.TryGet(conversationId, out var conversation));
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
                "deleted",
                indeterminate[0].Content,
                StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(indeterminate[0].CorrelationId);

            gate.TrySetResult("eventually");
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var active = surface.Session.TryGetActiveRunSnapshot(conversationId);
                if (active is null || active.Status is AgentRunStatus.Completed
                        or AgentRunStatus.Cancelled
                        or AgentRunStatus.Failed
                        or AgentRunStatus.Indeterminate)
                {
                    break;
                }

                await Task.Yield();
            }

            await surface.ViewModel.EndSessionCommand.Execute().ToTask();
            Assert.Null(surface.Session.TryGetSessionSnapshot(conversationId));
            Assert.True(surface.Store.TryGet(conversationId, out var endedConversation));
            Assert.Contains(
                endedConversation!.Entries,
                e => e.Content.StartsWith(
                    AgentConversationEventProjection.SessionEndedContentPrefix,
                    StringComparison.Ordinal));
            await ObserveOptionalAsync(sendTask);
        }
        finally
        {
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
    public async Task AcpCancellation_Success_UsesNonCancelledIndependentToken()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelTokenObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AcpFakeSessionClient(new AcpFakeSessionScript())
        {
            PromptHoldAsync = HoldUntilCancelled(holdEntered),
            CancelPromptAsyncOverride = (_, cancelToken) =>
            {
                cancelTokenObserved.TrySetResult(cancelToken.IsCancellationRequested);
                cancelToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
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
        Assert.False(await cancelTokenObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.False(client.LastCancelTokenWasCancellationRequested);
        Assert.Equal(client.ActiveSessionId, client.LastCancelSessionId);
        Assert.Contains(
            events,
            e => e.Kind == AgentBackendEventKind.FailureObserved
                 && e.Payload is AgentBackendFailurePayload failure
                 && failure.FailureKind == AgentFailureKind.Cancellation
                 && !failure.CancellationAcknowledgementUncertain);
    }

    [Fact]
    public async Task AcpCancelTimeout_ProducesAcknowledgementIndeterminate_RetainsOwnership_NoSessionEnded()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, cancelToken) =>
            {
                cancelEntered.TrySetResult();
                Assert.False(cancelToken.IsCancellationRequested);
                return Task.FromException(new OperationCanceledException(cancelToken));
            },
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp hold");

        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var endResult = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await cancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await ObserveOptionalAsync(sendTask);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, endResult.Status);
        Assert.NotNull(endResult.AttemptCorrelation);
        Assert.NotNull(endResult.SessionId);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));
        Assert.Contains(
            "timed out",
            endResult.Reason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "deleted",
            endResult.Reason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            endResult.AttemptCorrelation!.Value.Value,
            endResult.Reason ?? string.Empty,
            StringComparison.Ordinal);

        AgentConversationEventProjection.ProjectTerminationIndeterminate(
            harness.Store,
            harness.Conversation.Id,
            harness.AgentActor,
            endResult.Reason!,
            endResult.AttemptCorrelation);
        Assert.Single(
            SystemEntries(
                harness.Conversation,
                AgentConversationEventProjection.TerminationIndeterminateContentPrefix));
    }

    [Fact]
    public async Task AcpCancelFailure_PreservesTruthfulIndeterminateBoundary()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, _) => Task.FromException(new AcpProtocolException("simulated cancel failure")),
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp hold fail");

        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var endResult = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, endResult.Status);
        Assert.NotNull(endResult.AttemptCorrelation);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.Contains(
            "failed",
            endResult.Reason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            "provider stopped",
            endResult.Reason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "deleted",
            endResult.Reason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcpCancelTimeout_ThenRetrySuccess_ReissuesAckAndEndsOnlyAfterSecondAck()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelAttempt = 0;
        var secondCancelTokenWasAlreadyCancelled = true;
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, cancelToken) =>
            {
                cancelAttempt++;
                if (cancelAttempt == 1)
                {
                    Assert.False(cancelToken.IsCancellationRequested);
                    return Task.FromException(new OperationCanceledException(cancelToken));
                }

                secondCancelTokenWasAlreadyCancelled = cancelToken.IsCancellationRequested;
                Assert.False(cancelToken.IsCancellationRequested);
                return Task.CompletedTask;
            },
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp timeout then success");
        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var first = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, first.Status);
        Assert.Equal(1, harness.Client.CancelPromptCallCount);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));

        var second = await harness.SessionService.EndAsync(harness.Conversation.Id);
        Assert.Equal(AgentSessionEndStatus.Ended, second.Status);
        Assert.Equal(2, harness.Client.CancelPromptCallCount);
        Assert.False(secondCancelTokenWasAlreadyCancelled);
        Assert.Null(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Contains(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));
        Assert.NotEqual(first.AttemptCorrelation, second.AttemptCorrelation);
    }

    [Fact]
    public async Task AcpCancelFailure_ThenRetrySuccess_ReissuesAckAndEndsOnlyAfterSecondAck()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelAttempt = 0;
        var secondTokenCancelled = true;
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, cancelToken) =>
            {
                cancelAttempt++;
                if (cancelAttempt == 1)
                {
                    return Task.FromException(new AcpProtocolException("first cancel fail"));
                }

                secondTokenCancelled = cancelToken.IsCancellationRequested;
                return Task.CompletedTask;
            },
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp fail then success");
        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var first = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, first.Status);
        Assert.Equal(1, harness.Client.CancelPromptCallCount);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));

        var second = await harness.SessionService.EndAsync(harness.Conversation.Id);
        Assert.Equal(AgentSessionEndStatus.Ended, second.Status);
        Assert.Equal(2, harness.Client.CancelPromptCallCount);
        Assert.False(secondTokenCancelled);
        Assert.Null(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Contains(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcpCancelTimeout_ThenTimeout_RetainsEnding_TwoDistinctIndeterminateAttempts()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelTokensNonCancelled = new List<bool>();
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, cancelToken) =>
            {
                cancelTokensNonCancelled.Add(!cancelToken.IsCancellationRequested);
                return Task.FromException(new OperationCanceledException(cancelToken));
            },
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp double timeout");
        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var first = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);
        var second = await harness.SessionService.EndAsync(harness.Conversation.Id);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, first.Status);
        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, second.Status);
        Assert.NotEqual(first.AttemptCorrelation, second.AttemptCorrelation);
        Assert.Equal(2, harness.Client.CancelPromptCallCount);
        Assert.All(cancelTokensNonCancelled, Assert.True);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));

        AgentConversationEventProjection.ProjectTerminationIndeterminate(
            harness.Store,
            harness.Conversation.Id,
            harness.AgentActor,
            first.Reason!,
            first.AttemptCorrelation);
        AgentConversationEventProjection.ProjectTerminationIndeterminate(
            harness.Store,
            harness.Conversation.Id,
            harness.AgentActor,
            first.Reason!,
            first.AttemptCorrelation);
        AgentConversationEventProjection.ProjectTerminationIndeterminate(
            harness.Store,
            harness.Conversation.Id,
            harness.AgentActor,
            second.Reason!,
            second.AttemptCorrelation);
        Assert.Equal(
            2,
            SystemEntries(
                harness.Conversation,
                AgentConversationEventProjection.TerminationIndeterminateContentPrefix).Count());
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
    }

    [Fact]
    public async Task AcpCancelFailure_ThenFailure_RetainsEnding_NoSessionEnded()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, _) => Task.FromException(new AcpProtocolException("cancel still failing")),
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp double fail");
        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var first = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);
        var second = await harness.SessionService.EndAsync(harness.Conversation.Id);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, first.Status);
        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, second.Status);
        Assert.NotEqual(first.AttemptCorrelation, second.AttemptCorrelation);
        Assert.Equal(2, harness.Client.CancelPromptCallCount);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            "deleted",
            second.Reason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        AgentConversationEventProjection.ProjectTerminationIndeterminate(
            harness.Store,
            harness.Conversation.Id,
            harness.AgentActor,
            first.Reason!,
            first.AttemptCorrelation);
        AgentConversationEventProjection.ProjectTerminationIndeterminate(
            harness.Store,
            harness.Conversation.Id,
            harness.AgentActor,
            second.Reason!,
            second.AttemptCorrelation);
        Assert.Equal(
            2,
            SystemEntries(
                harness.Conversation,
                AgentConversationEventProjection.TerminationIndeterminateContentPrefix).Count());
    }

    [Fact]
    public async Task AcpRetry_DoesNotFinalizeMerelyBecauseOriginalRunIsTerminalIndeterminate()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, cancelToken) => Task.FromException(new OperationCanceledException(cancelToken)),
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "terminal indeterminate is not ack");
        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        var first = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, first.Status);
        Assert.Null(harness.SessionService.TryGetActiveRunSnapshot(harness.Conversation.Id));
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));

        var second = await harness.SessionService.EndAsync(harness.Conversation.Id);
        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, second.Status);
        Assert.Equal(2, harness.Client.CancelPromptCallCount);
        Assert.NotNull(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcpIndeterminateRetry_KeepsTownhallEndSessionEnabledUntilAckSucceeds()
    {
        var holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelAttempt = 0;
        using var harness = CreateAcpEndSurface(
            HoldUntilCancelled(holdEntered),
            (_, cancelToken) =>
            {
                cancelAttempt++;
                if (cancelAttempt == 1)
                {
                    return Task.FromException(new OperationCanceledException(cancelToken));
                }

                return Task.CompletedTask;
            },
            endAcknowledgementTimeout: TimeSpan.FromSeconds(5));

        var draftState = ConversationsTestSupport.CreateDraftState();
        var host = ConversationsTestSupport.CreatePanelHost(harness.Catalog, harness.Store, draftState);
        var (coordinator, _, _) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            harness.Store,
            draftState,
            catalog: harness.Catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, harness.Catalog, harness.Store);
        using var vm = ConversationsTestSupport.CreateTownhallViewModel(
            state: new TownhallState(),
            catalog: harness.Catalog,
            store: harness.Store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: new TownhallConversationUiState(draftState),
            draftState: draftState,
            agentRouter: router,
            sessionService: harness.SessionService);

        var sendTask = harness.SessionService.SendAsync(
            harness.Conversation.Id,
            ActorId.HumanUser,
            harness.AgentActor,
            harness.Backend.BackendId,
            ConversationEntryId.New(),
            "acp townhall availability");
        await holdEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForActiveRunAsync(harness.SessionService, harness.Conversation.Id);

        await SelectConversationAsync(vm, harness.Conversation.Id);
        await WaitForCanEndSessionAsync(vm, expected: true);

        var first = await harness.SessionService.EndAsync(harness.Conversation.Id);
        await ObserveOptionalAsync(sendTask);

        Assert.Equal(AgentSessionEndStatus.AcknowledgementIndeterminate, first.Status);
        await WaitForCanEndSessionAsync(vm, expected: true);
        Assert.True(vm.CanEndSession);
        Assert.Equal(
            AgentSessionStatus.Ending,
            harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id)!.Status);
        Assert.DoesNotContain(
            harness.Conversation.Entries,
            e => e.Content.StartsWith(
                AgentConversationEventProjection.SessionEndedContentPrefix,
                StringComparison.Ordinal));

        var second = await harness.SessionService.EndAsync(harness.Conversation.Id);
        Assert.Equal(AgentSessionEndStatus.Ended, second.Status);
        Assert.Equal(2, harness.Client.CancelPromptCallCount);
        await WaitForCanEndSessionAsync(vm, expected: false);
        Assert.False(vm.CanEndSession);
        Assert.Null(harness.SessionService.TryGetSessionSnapshot(harness.Conversation.Id));
    }

    [Fact]
    public void Projection_RepeatedIndeterminateForSameAttempt_IsDeduplicated()
    {
        var store = ConversationsTestSupport.CreateStore();
        var conversation = store.GetOrCreateDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"));
        var correlation = ConversationEntryCorrelationId.FromValue("term-attempt|sess-a|run-a|attempt-1");
        var reason =
            "Backend acknowledgement timed out. Retry is available. Provider termination is not claimed.";

        var first = AgentConversationEventProjection.ProjectTerminationIndeterminate(
            store,
            conversation.Id,
            ActorId.PanelSeed("alpha"),
            reason,
            correlation);
        var second = AgentConversationEventProjection.ProjectTerminationIndeterminate(
            store,
            conversation.Id,
            ActorId.PanelSeed("alpha"),
            reason,
            correlation);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(
            SystemEntries(
                conversation,
                AgentConversationEventProjection.TerminationIndeterminateContentPrefix));
        Assert.Equal(correlation, first.CorrelationId);
        Assert.DoesNotContain(correlation.Value, first.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Projection_DistinctSessionsOrAttempts_EachReceiveOwnIndeterminateEntry()
    {
        var store = ConversationsTestSupport.CreateStore();
        var conversation = store.GetOrCreateDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"));
        var reason =
            "Cancel acknowledgement was indeterminate. Retry is available. Provider termination is not claimed.";
        var attemptA = ConversationEntryCorrelationId.FromValue("term-attempt|session-1|run-1|a");
        var attemptB = ConversationEntryCorrelationId.FromValue("term-attempt|session-2|run-2|b");

        var first = AgentConversationEventProjection.ProjectTerminationIndeterminate(
            store,
            conversation.Id,
            ActorId.PanelSeed("alpha"),
            reason,
            attemptA);
        var second = AgentConversationEventProjection.ProjectTerminationIndeterminate(
            store,
            conversation.Id,
            ActorId.PanelSeed("alpha"),
            reason,
            attemptB);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, SystemEntries(
            conversation,
            AgentConversationEventProjection.TerminationIndeterminateContentPrefix).Count());
        Assert.Equal(attemptA, first.CorrelationId);
        Assert.Equal(attemptB, second.CorrelationId);
        Assert.DoesNotContain(attemptA.Value, first.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(attemptB.Value, second.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndAsync_NoLiveSession_ReturnsNoLiveSessionWithoutProviderClaims()
    {
        using var session = new AgentSessionService(
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

    [Fact]
    public async Task Townhall_EndSessionCommand_IsReachable_AndPanelControlSurfacesWhenEnabled()
    {
        using var surface = CreateSurface();
        await OpenDirectAsync(surface.ViewModel, ActorId.PanelSeed("alpha"));

        Assert.NotNull(surface.ViewModel.EndSessionCommand);
        Assert.False(surface.ViewModel.CanEndSession);

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
}
