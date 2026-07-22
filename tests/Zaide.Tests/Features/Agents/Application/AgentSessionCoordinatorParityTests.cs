using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.App.Composition.Registration;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Tests.App.Composition;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 15 M3b-1 coordinator/router session cutover parity tests.
/// </summary>
public sealed class AgentSessionCoordinatorParityTests
{
    private static readonly AgentBackendId LegacyBackendId =
        AgentBackendId.FromValue(LegacyOpenAiCompatibleAgentBackend.BackendIdValue);

    private static (AgentPanelHost Host, AgentPanelState Panel, ConversationStore Store, FakeAgentBackend Backend, IAgentSessionService Session)
        CreateSurface()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        _ = coordinator;
        return (host, panel, store, backend, session);
    }

    [Fact]
    public async Task DirectSend_UsesSessionServiceAndLegacyCompatibilityBackend()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetCompletion("session reply");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        var result = await coordinator.SendAsync(panel.PanelId, "hello");

        Assert.Equal(1, backend.ExecuteCallCount);
        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.Success, result!.Run.Outcome);
        Assert.Equal("session reply", result.AssistantResponse);
    }

    [Fact]
    public async Task MentionRouting_ExecutesOnTargetPrivateConversation()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var source = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var target = host.GetOrCreatePanelForActor(ActorId.PanelSeed("beta"));
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        backend.SetCompletion("target reply");
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        var route = await router.RouteAndExecuteAsync(source.PanelId, "@Beta routed text");

        Assert.True(route.Success);
        Assert.Empty(source.OutputHistory);
        Assert.Equal(2, target.OutputHistory.Count);
        Assert.True(store.TryGetDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("beta"),
            out var betaConversation));
        Assert.Contains(
            betaConversation!.Entries,
            e => e.Kind == ConversationEntryKind.UserChat && e.Content == "routed text");
    }

    [Fact]
    public void OpeningConversation_DoesNotCreateSessionOrInvokeBackend()
    {
        var (host, panel, _, backend, session) = CreateSurface();

        Assert.Equal(0, backend.ExecuteCallCount);
        Assert.Null(session.TryGetSessionSnapshot(panel.ConversationId));
        Assert.Null(session.TryGetActiveRunSnapshot(panel.ConversationId));
    }

    [Fact]
    public async Task AdmittedSend_ProducesStableRunIdAcrossSessionEventsSnapshotAndEntries()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetCompletion("stable");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        var events = new List<AgentEvent>();
        using var subscription = session.Events.Subscribe(events.Add);

        var result = await coordinator.SendAsync(panel.PanelId, "hello");

        Assert.NotNull(result);
        var runId = result!.Run.Id;
        Assert.All(
            events.Where(e => e.Kind != AgentEventKind.CapabilitySnapshotChanged),
            e => Assert.Equal(runId, e.RunId));
        Assert.NotNull(session.TryGetSessionSnapshot(panel.ConversationId));
        Assert.Equal(AgentSessionStatus.Ready, session.TryGetSessionSnapshot(panel.ConversationId)!.Status);

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.All(
            conversation!.Entries.Where(e => e.CorrelationId is not null),
            e => Assert.Equal(runId.Value, e.CorrelationId!.Value.Value));
        Assert.Equal(runId, result.Run.Id);
    }

    [Fact]
    public async Task CompatibilityProjection_PreservesUserBeforeTerminalEntryOrdering()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetCompletion("ordered");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        await coordinator.SendAsync(panel.PanelId, "first");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);
        Assert.Equal("User: first", panel.OutputHistory[0]);
        Assert.Equal("Assistant: ordered", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task UserMessageAdmitted_IsVisibleBeforeBlockedBackendCompletes()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "late");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        var sendTask = coordinator.SendAsync(panel.PanelId, "blocked");
        await WaitUntilAsync(
            () => store.TryGet(panel.ConversationId, out var conversation)
                && conversation!.Entries.Any(e => e.Kind == ConversationEntryKind.UserChat));
        Assert.Equal(1, backend.ExecuteCallCount);
        Assert.False(sendTask.IsCompleted);
        Assert.True(store.TryGet(panel.ConversationId, out var inFlight));
        Assert.Single(inFlight!.Entries, e => e.Kind == ConversationEntryKind.UserChat);

        await sendTask;
    }

    [Fact]
    public async Task SuccessfulCompletion_ReturnsExactAssistantText()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetCompletion("exact assistant");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        var result = await coordinator.SendAsync(panel.PanelId, "hello");

        Assert.NotNull(result);
        Assert.Equal("exact assistant", result!.AssistantResponse);
    }

    [Fact]
    public async Task ExecutionFailure_MapsFromTypedSessionTruth()
    {
        await AssertTerminalFailureMappingAsync(
            AgentFailureKind.Execution,
            ExecutionRunOutcome.ExecutionFailure);
    }

    [Fact]
    public async Task Timeout_MapsFromTypedSessionTruth()
    {
        await AssertTerminalFailureMappingAsync(
            AgentFailureKind.Timeout,
            ExecutionRunOutcome.ExecutionFailure);
    }

    [Fact]
    public async Task Transport_MapsFromTypedSessionTruth()
    {
        await AssertTerminalFailureMappingAsync(
            AgentFailureKind.Transport,
            ExecutionRunOutcome.ExecutionFailure);
    }

    [Fact]
    public async Task Indeterminate_MapsFromTypedSessionTruth()
    {
        await AssertTerminalFailureMappingAsync(
            AgentFailureKind.Indeterminate,
            ExecutionRunOutcome.ExecutionFailure);
    }

    private static async Task AssertTerminalFailureMappingAsync(
        AgentFailureKind failureKind,
        ExecutionRunOutcome expectedOutcome)
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetFailure(failureKind, "typed reason");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        var result = await coordinator.SendAsync(panel.PanelId, "hello");

        Assert.NotNull(result);
        Assert.Equal(expectedOutcome, result!.Run.Outcome);
        Assert.Equal("typed reason", result.ErrorMessage);
        Assert.DoesNotContain("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timeout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreCancelledToken_ReturnsStructuredTerminalResultWithoutThrowing()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "never");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await coordinator.SendAsync(panel.PanelId, "cancelled early", cts.Token);

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.Cancelled, result!.Run.Outcome);
    }

    [Fact]
    public async Task CancellationBetweenInvocationAndAdmission_ReturnsStructuredTerminalResult()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "late");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        using var cts = new CancellationTokenSource();
        var kinds = new List<AgentEventKind>();
        ExecutionRunId? admittedRunId = null;
        using var subscription = session.Events.Subscribe(agentEvent =>
        {
            kinds.Add(agentEvent.Kind);
            if (agentEvent.Kind == AgentEventKind.UserMessageAdmitted)
            {
                admittedRunId = agentEvent.RunId;
            }
        });

        var sendTask = coordinator.SendAsync(panel.PanelId, "cancel during admission", cts.Token);
        await WaitUntilAsync(() =>
            session.TryGetActiveRunSnapshot(panel.ConversationId)?.Status == AgentRunStatus.Running
            || kinds.Contains(AgentEventKind.RunCancellationRequested));
        cts.Cancel();

        var result = await sendTask;

        Assert.NotNull(result);
        Assert.NotNull(admittedRunId);
        Assert.Equal(admittedRunId, result!.Run.Id);
        Assert.Equal(ExecutionRunOutcome.Cancelled, result.Run.Outcome);
        Assert.Null(session.TryGetActiveRunSnapshot(panel.ConversationId));
        Assert.True(kinds.IndexOf(AgentEventKind.RunCancellationRequested) <
                    kinds.LastIndexOf(AgentEventKind.RunCancelled));
        Assert.DoesNotContain(AgentEventKind.RunCompleted, kinds);
        Assert.False(coordinator.IsConversationBusy(panel.ConversationId));
    }

    [Fact]
    public async Task Cancellation_MapsFromTypedSessionStatus_NotErrorMessageParsing()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "never");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        using var cts = new CancellationTokenSource();
        var kinds = new List<AgentEventKind>();
        using var subscription = session.Events.Subscribe(e => kinds.Add(e.Kind));

        var sendTask = coordinator.SendAsync(panel.PanelId, "cancel me", cts.Token);
        await WaitForRunningAsync(session, panel.ConversationId);
        cts.Cancel();

        var result = await sendTask;

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.Cancelled, result!.Run.Outcome);
        Assert.True(kinds.IndexOf(AgentEventKind.RunCancellationRequested) <
                    kinds.LastIndexOf(AgentEventKind.RunCancelled));
    }

    [Fact]
    public async Task AdmissionRejection_ReturnsStructuredOutcomeNotNullOrCancellation()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "busy");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        _ = coordinator.SendAsync(panel.PanelId, "first");
        await Task.Delay(50);
        var rejected = await coordinator.SendAsync(panel.PanelId, "second");

        Assert.NotNull(rejected);
        Assert.Equal(ExecutionRunOutcome.Rejected, rejected!.Run.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(rejected.ErrorMessage));
    }

    [Fact]
    public async Task RejectedConcurrentSend_DoesNotCreateUserEntryOrSecondBackendExecution()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "winner");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        var busyChanges = new List<bool>();
        coordinator.ConversationBusyChanged += (_, isBusy) => busyChanges.Add(isBusy);

        var first = coordinator.SendAsync(panel.PanelId, "first");
        await WaitForRunningAsync(session, panel.ConversationId);
        var firstRunId = session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId;

        var second = await coordinator.SendAsync(panel.PanelId, "second");

        Assert.Equal(ExecutionRunOutcome.Rejected, second!.Run.Outcome);
        Assert.Equal(firstRunId, session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId);
        Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
        Assert.True(panel.IsBusy);
        Assert.Equal(new[] { true }, busyChanges);
        Assert.Equal(1, backend.ExecuteCallCount);
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Single(conversation!.Entries, e => e.Kind == ConversationEntryKind.UserChat);

        await first;

        Assert.Equal(new[] { true, false }, busyChanges);
        Assert.False(coordinator.IsConversationBusy(panel.ConversationId));
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task TerminalReadmission_StaleFinalizerDoesNotClearNewerAdmittedRun()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        var firstGate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(firstGate, "first");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        var busyChanges = new List<bool>();
        coordinator.ConversationBusyChanged += (_, isBusy) => busyChanges.Add(isBusy);

        var releaseRunATerminalProjection = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AgentExecutionCoordinatorResult?>? second = null;
        ExecutionRunId? secondRunId = null;

        var first = coordinator.SendAsync(panel.PanelId, "first");
        await WaitForRunningAsync(session, panel.ConversationId);
        var firstRunId = session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId;

        coordinator.OnBeforeTerminalPanelProjectionAsync = async (conversationId, runId) =>
        {
            if (runId != firstRunId)
            {
                return;
            }

            backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "second");
            second = coordinator.SendAsync(panel.PanelId, "second");
            await WaitUntilAsync(() =>
                session.TryGetActiveRunSnapshot(conversationId)?.RunId != firstRunId);
            secondRunId = session.TryGetActiveRunSnapshot(conversationId)!.RunId;
            await releaseRunATerminalProjection.Task;
        };

        try
        {
            firstGate.SetResult("first");
            await WaitUntilAsync(() => secondRunId is not null);

            Assert.NotEqual(firstRunId, secondRunId);
            Assert.Equal(secondRunId, session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId);
            Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
            Assert.True(panel.IsBusy);
            Assert.Equal("Thinking", panel.Status);
            Assert.Equal(new[] { true }, busyChanges);

            releaseRunATerminalProjection.SetResult();

            var firstResult = await first;
            Assert.Equal(ExecutionRunOutcome.Success, firstResult!.Run.Outcome);
            Assert.Equal(firstRunId, firstResult.Run.Id);
            Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
            Assert.True(panel.IsBusy);
            Assert.Equal("Thinking", panel.Status);
            Assert.Equal(new[] { true }, busyChanges);

            var secondResult = await second!;
            Assert.Equal(ExecutionRunOutcome.Success, secondResult!.Run.Outcome);
            Assert.Equal(secondRunId, secondResult.Run.Id);
            Assert.False(coordinator.IsConversationBusy(panel.ConversationId));
            Assert.False(panel.IsBusy);
            Assert.Equal(new[] { true, false }, busyChanges);
        }
        finally
        {
            coordinator.OnBeforeTerminalPanelProjectionAsync = null;
        }
    }

    [Fact]
    public async Task TerminalPanelProjection_StaleRunDoesNotSetIdleWhileNewerRunOwns()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        var firstGate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(firstGate, "first");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        var releaseRunATerminalProjection = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AgentExecutionCoordinatorResult?>? second = null;
        ExecutionRunId? secondRunId = null;

        var first = coordinator.SendAsync(panel.PanelId, "first");
        await WaitForRunningAsync(session, panel.ConversationId);
        var firstRunId = session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId;

        coordinator.OnBeforeTerminalPanelProjectionAsync = async (conversationId, runId) =>
        {
            if (runId != firstRunId)
            {
                return;
            }

            backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "second");
            second = coordinator.SendAsync(panel.PanelId, "second");
            await WaitUntilAsync(() =>
                session.TryGetActiveRunSnapshot(conversationId)?.RunId != firstRunId);
            secondRunId = session.TryGetActiveRunSnapshot(conversationId)!.RunId;
            await releaseRunATerminalProjection.Task;
        };

        try
        {
            firstGate.SetResult("first");
            await WaitUntilAsync(() => secondRunId is not null);

            Assert.Equal(secondRunId, session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId);
            Assert.True(panel.IsBusy);
            Assert.Equal("Thinking", panel.Status);

            releaseRunATerminalProjection.SetResult();

            var firstResult = await first;
            Assert.Equal(ExecutionRunOutcome.Success, firstResult!.Run.Outcome);
            Assert.True(panel.IsBusy);
            Assert.Equal("Thinking", panel.Status);
            Assert.NotEqual("Idle", panel.Status);
            Assert.NotEqual("Error", panel.Status);

            var secondResult = await second!;
            Assert.Equal(ExecutionRunOutcome.Success, secondResult!.Run.Outcome);
            Assert.False(panel.IsBusy);
            Assert.Equal("Idle", panel.Status);
        }
        finally
        {
            coordinator.OnBeforeTerminalPanelProjectionAsync = null;
        }
    }

    [Fact]
    public async Task TerminalReadmission_StaleBusyFalseIsSuppressedWhileNewerRunIsActive()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        var firstGate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(firstGate, "first");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        var busyChanges = new List<bool>();
        coordinator.ConversationBusyChanged += (_, isBusy) => busyChanges.Add(isBusy);

        var releaseRunAFinalizer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AgentExecutionCoordinatorResult?>? second = null;
        ExecutionRunId? secondRunId = null;

        var first = coordinator.SendAsync(panel.PanelId, "first");
        await WaitForRunningAsync(session, panel.ConversationId);
        var firstRunId = session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId;

        coordinator.OnAfterInFlightRemovalBeforeBusyNotificationAsync = async (conversationId, runId) =>
        {
            if (runId != firstRunId)
            {
                return;
            }

            backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "second");
            second = coordinator.SendAsync(panel.PanelId, "second");
            await WaitUntilAsync(() =>
                session.TryGetActiveRunSnapshot(conversationId)?.RunId != firstRunId);
            secondRunId = session.TryGetActiveRunSnapshot(conversationId)!.RunId;
            await releaseRunAFinalizer.Task;
        };

        try
        {
            firstGate.SetResult("first");
            await WaitUntilAsync(() => secondRunId is not null);

            Assert.Equal(secondRunId, session.TryGetActiveRunSnapshot(panel.ConversationId)!.RunId);
            Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
            Assert.True(panel.IsBusy);
            Assert.Equal("Thinking", panel.Status);
            Assert.Equal(new[] { true }, busyChanges);

            releaseRunAFinalizer.SetResult();

            var firstResult = await first;
            Assert.Equal(ExecutionRunOutcome.Success, firstResult!.Run.Outcome);
            Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
            Assert.True(panel.IsBusy);
            Assert.Equal("Thinking", panel.Status);
            Assert.Equal(new[] { true }, busyChanges);

            var secondResult = await second!;
            Assert.Equal(ExecutionRunOutcome.Success, secondResult!.Run.Outcome);
            Assert.False(coordinator.IsConversationBusy(panel.ConversationId));
            Assert.False(panel.IsBusy);
            Assert.Equal(new[] { true, false }, busyChanges);
        }
        finally
        {
            coordinator.OnAfterInFlightRemovalBeforeBusyNotificationAsync = null;
        }
    }

    [Fact]
    public async Task BusyNotificationDrain_SerializesCallbacksWhileFirstConversationIsBlocked()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panelA = host.CreatePanel("agent-a", "Alpha", "avatar_a");
        var panelB = host.CreatePanel("agent-b", "Beta", "avatar_b");
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        var gateA = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gateA, "alpha");

        var callbackOrder = new List<(ConversationId Id, bool IsBusy)>();
        var orderLock = new object();
        var aCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bCallbackObserved = 0;

        coordinator.ConversationBusyChanged += (id, isBusy) =>
        {
            lock (orderLock)
            {
                callbackOrder.Add((id, isBusy));
            }

            if (id == panelA.ConversationId && isBusy)
            {
                aCallbackEntered.TrySetResult();
                releaseA.Task.GetAwaiter().GetResult();
            }

            if (id == panelB.ConversationId && isBusy)
            {
                Interlocked.Exchange(ref bCallbackObserved, 1);
            }
        };

        var sendA = Task.Run(async () => await coordinator.SendAsync(panelA.PanelId, "alpha"));
        await aCallbackEntered.Task;

        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "beta");
        var sendB = Task.Run(async () => await coordinator.SendAsync(panelB.PanelId, "beta"));
        await WaitForRunningAsync(session, panelB.ConversationId);

        Assert.Equal(0, Volatile.Read(ref bCallbackObserved));
        lock (orderLock)
        {
            Assert.Single(callbackOrder);
            Assert.Equal((panelA.ConversationId, true), callbackOrder[0]);
        }

        releaseA.SetResult();
        gateA.SetResult("alpha");

        await Task.WhenAll(sendA, sendB);

        lock (orderLock)
        {
            Assert.True(callbackOrder.Count >= 2);
            Assert.Equal((panelA.ConversationId, true), callbackOrder[0]);
            Assert.Equal((panelB.ConversationId, true), callbackOrder[1]);
        }
    }

    [Fact]
    public async Task BusyNotificationDrain_ResetsDrainerAndContinuesAfterSubscriberThrows()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panelA = host.CreatePanel("agent-a", "Alpha", "avatar_a");
        var panelB = host.CreatePanel("agent-b", "Beta", "avatar_b");
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(200), "alpha");

        var healthyCallbacks = new List<(ConversationId Id, bool IsBusy)>();
        var orderLock = new object();
        var aCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseThrow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bCallbackObserved = 0;
        Exception? escapedSubscriberFault = null;

        coordinator.ConversationBusyChanged += (id, isBusy) =>
        {
            if (id == panelA.ConversationId && isBusy)
            {
                aCallbackEntered.TrySetResult();
                releaseThrow.Task.GetAwaiter().GetResult();
                var fault = new InvalidOperationException("Subscriber fault during busy notification.");
                escapedSubscriberFault = fault;
                throw fault;
            }

            if (id == panelB.ConversationId && isBusy)
            {
                Interlocked.Exchange(ref bCallbackObserved, 1);
            }
        };

        coordinator.ConversationBusyChanged += (id, isBusy) =>
        {
            lock (orderLock)
            {
                healthyCallbacks.Add((id, isBusy));
            }
        };

        var sendA = Task.Run(async () => await coordinator.SendAsync(panelA.PanelId, "alpha"));
        await aCallbackEntered.Task;

        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(200), "beta");
        var sendB = Task.Run(async () => await coordinator.SendAsync(panelB.PanelId, "beta"));
        await WaitForRunningAsync(session, panelB.ConversationId);

        Assert.Equal(0, Volatile.Read(ref bCallbackObserved));
        lock (orderLock)
        {
            Assert.Empty(healthyCallbacks);
        }

        releaseThrow.SetResult();

        var resultA = await sendA;
        await sendB;

        Assert.NotNull(escapedSubscriberFault);
        Assert.Equal(ExecutionRunOutcome.Success, resultA!.Run.Outcome);
        Assert.False(coordinator.IsConversationBusy(panelA.ConversationId));
        Assert.Null(session.TryGetActiveRunSnapshot(panelA.ConversationId));
        Assert.False(panelA.IsBusy);
        Assert.Equal(1, Volatile.Read(ref bCallbackObserved));
        lock (orderLock)
        {
            Assert.True(healthyCallbacks.Count >= 2);
            Assert.Equal((panelA.ConversationId, true), healthyCallbacks[0]);
            Assert.Equal((panelB.ConversationId, true), healthyCallbacks[1]);
        }
    }

    [Fact]
    public async Task Concurrency_AllowsIndependentConversations()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        backend.SetCompletion("one", "two");

        await Task.WhenAll(
            coordinator.SendAsync(panel1.PanelId, "one"),
            coordinator.SendAsync(panel2.PanelId, "two"));

        Assert.Equal(2, backend.ExecuteCallCount);
        Assert.Equal(2, panel1.OutputHistory.Count);
        Assert.Equal(2, panel2.OutputHistory.Count);
    }

    [Fact]
    public async Task BusySignals_RemainTruthfulForAdmittedRuns()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(250), "done");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        var busyChanges = new List<(ConversationId Id, bool IsBusy)>();
        coordinator.ConversationBusyChanged += (id, isBusy) => busyChanges.Add((id, isBusy));

        var sendTask = coordinator.SendAsync(panel.PanelId, "busy");
        await WaitForRunningAsync(session, panel.ConversationId);

        Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
        Assert.True(panel.IsBusy);
        Assert.Equal("Thinking", panel.Status);

        await sendTask;

        Assert.False(coordinator.IsConversationBusy(panel.ConversationId));
        Assert.False(panel.IsBusy);
        Assert.Equal("Idle", panel.Status);
        Assert.Equal(new[] { (panel.ConversationId, true), (panel.ConversationId, false) }, busyChanges);
    }

    [Fact]
    public async Task PanelCloseDuringExecution_DoesNotCancelRunOrLoseHistory()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gate, "after close");
        var coordinator = new AgentExecutionCoordinator(host, session, store);

        var sendTask = coordinator.SendAsync(panel.PanelId, "stay alive");
        await WaitForRunningAsync(session, panel.ConversationId);
        host.ClosePanel(panel.PanelId);

        Assert.True(coordinator.IsConversationBusy(panel.ConversationId));
        gate.SetResult("after close");
        var result = await sendTask;

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.Success, result!.Run.Outcome);
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
    }

    [Fact]
    public async Task CallerCancellation_EmitsCancellationRequestedBeforeTerminalAndClearsBusyOnce()
    {
        var (host, panel, store, backend, session) = CreateSurface();
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "late");
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        using var cts = new CancellationTokenSource();
        var busyChanges = new List<bool>();
        coordinator.ConversationBusyChanged += (_, isBusy) => busyChanges.Add(isBusy);

        var sendTask = coordinator.SendAsync(panel.PanelId, "cancel", cts.Token);
        await WaitForRunningAsync(session, panel.ConversationId);
        cts.Cancel();
        await sendTask;

        Assert.Equal(new[] { true, false }, busyChanges);
        Assert.False(coordinator.IsConversationBusy(panel.ConversationId));
    }

    [Fact]
    public async Task EventCapture_IsSubscribedBeforeAdmission_FilteredAndThreadSafeAcrossConversations()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        backend.SetCompletion("a", "b");

        var firstTask = coordinator.SendAsync(panel1.PanelId, "a");
        var secondTask = coordinator.SendAsync(panel2.PanelId, "b");
        var first = await firstTask;
        var second = await secondTask;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.Run.Id, second!.Run.Id);
        Assert.True(store.TryGet(panel1.ConversationId, out var c1));
        Assert.True(store.TryGet(panel2.ConversationId, out var c2));
        Assert.All(c1!.Entries, e => Assert.Equal(first!.Run.Id.Value, e.CorrelationId?.Value));
        Assert.All(c2!.Entries, e => Assert.Equal(second!.Run.Id.Value, e.CorrelationId?.Value));
    }

    [Fact]
    public async Task RoutingFailureBeforeAdmission_RetainsExactOwnershipAndFrozenStrings()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var panel = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        var route = await router.RouteAndExecuteAsync(panel.PanelId, "@Ghost missing");

        Assert.False(route.Success);
        Assert.Equal(0, backend.ExecuteCallCount);
        Assert.Null(session.TryGetSessionSnapshot(panel.ConversationId));
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Contains(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.RoutingFailure
                 && e.Content == "Unknown target");
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, route.ExecutionResult!.Run.Outcome);
    }

    [Fact]
    public void Di_ProvesCoordinatorUsesSessionServiceNotExecutionService()
    {
        using var provider = AgentsRegistrationModuleTests.BuildProductionProvider();

        var coordinator = provider.GetRequiredService<IAgentExecutionCoordinator>();
        var session = provider.GetRequiredService<IAgentSessionService>();
        var backend = provider.GetRequiredService<IAgentBackend>();

        Assert.IsType<AgentExecutionCoordinator>(coordinator);
        Assert.IsType<AgentSessionService>(session);
        Assert.IsType<LegacyOpenAiCompatibleAgentBackend>(backend);

        var constructor = typeof(AgentExecutionCoordinator).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().Length == 4);
        var parameters = constructor.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(IAgentSessionService), parameters);
        Assert.DoesNotContain(typeof(IAgentExecutionService), parameters);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int maxAttempts = 100)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Timed out waiting for condition.");
    }

    private static async Task WaitForRunningAsync(
        IAgentSessionService session,
        ConversationId conversationId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var run = session.TryGetActiveRunSnapshot(conversationId);
            if (run?.Status == AgentRunStatus.Running)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Timed out waiting for running session.");
    }
}
