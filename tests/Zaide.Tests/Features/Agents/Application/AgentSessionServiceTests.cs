using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class AgentSessionServiceTests
{
    private static readonly AgentBackendId TestBackendId =
        AgentBackendId.FromValue("backend:legacy-openai-compatible");

    [Fact]
    public void TryGetSessionSnapshot_BeforeSend_ReturnsNull()
    {
        var service = CreateService(out _);
        var conversationId = ConversationId.NewDirect();

        Assert.Null(service.TryGetSessionSnapshot(conversationId));
        Assert.Null(service.TryGetActiveRunSnapshot(conversationId));
    }

    [Fact]
    public async Task SendAsync_FirstSendCreatesSession_SecondSendReusesSession()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("first", "second");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");

        var first = await SendAsync(service, conversationId, target, "first");
        var sessionAfterFirst = service.TryGetSessionSnapshot(conversationId);
        Assert.NotNull(sessionAfterFirst);
        var sessionId = sessionAfterFirst!.SessionId;

        var second = await SendAsync(service, conversationId, target, "second");

        var sessionAfterSecond = service.TryGetSessionSnapshot(conversationId);
        Assert.NotNull(sessionAfterSecond);
        Assert.Equal(sessionId, sessionAfterSecond!.SessionId);
        Assert.Equal(AgentRunStatus.Completed, first.Status);
        Assert.Equal(AgentRunStatus.Completed, second.Status);
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal(2, backend.ExecuteCallCount);
    }

    [Fact]
    public async Task SendAsync_DoesNotInvokeBackendExecuteDuringSessionAdmissionSetup()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("hello");
        var conversationId = ConversationId.NewDirect();

        Assert.Equal(0, backend.ExecuteCallCount);
        _ = await SendAsync(service, conversationId, ActorId.PanelSeed("alpha"), "hello");
        Assert.Equal(1, backend.ExecuteCallCount);
    }

    [Fact]
    public async Task SendAsync_ConcurrentAdmission_AllowsExactlyOneActiveRun()
    {
        var service = CreateService(out var backend);
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(200), "winner");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");

        var firstTask = SendAsync(service, conversationId, target, "one");
        await Task.Delay(25);
        var secondTask = SendAsync(service, conversationId, target, "two");

        var results = await Task.WhenAll(firstTask, secondTask);
        var statuses = results.Select(r => r.Status).OrderBy(s => s.ToString()).ToArray();

        Assert.Contains(AgentRunStatus.Completed, statuses);
        Assert.Contains(AgentRunStatus.Rejected, statuses);
        Assert.Equal(1, backend.ExecuteCallCount);

        var rejected = results.Single(r => r.Status == AgentRunStatus.Rejected);
        Assert.Equal(AgentRunStatus.Rejected, rejected.Status);
    }

    [Fact]
    public async Task SendAsync_ConflictingRequest_ReturnsStructuredRejectionNotNull()
    {
        var service = CreateService(out var backend);
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(200), "busy");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");

        _ = SendAsync(service, conversationId, target, "first");
        await Task.Delay(25);

        var rejected = await SendAsync(service, conversationId, target, "second");

        Assert.NotNull(rejected);
        Assert.Equal(AgentRunStatus.Rejected, rejected.Status);
        Assert.NotEqual(default(ExecutionRunId), rejected.RunId);
        Assert.NotEqual(default(AgentSessionId), rejected.SessionId);
    }

    [Fact]
    public async Task SendAsync_FirstAdmission_EmitsExactEventOrderAndRunCorrelation()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("hello");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");
        var events = new List<AgentEvent>();
        using var subscription = service.Events.Subscribe(events.Add);

        var snapshot = await SendAsync(service, conversationId, target, "hello");

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);

        var admittedRunId = events.First(e => e.Kind == AgentEventKind.RunCreated).RunId;
        Assert.All(events, agentEvent => Assert.Equal(admittedRunId, agentEvent.RunId));

        Assert.Equal(
            new[]
            {
                AgentEventKind.RunCreated,
                AgentEventKind.SessionReady,
                AgentEventKind.CapabilitySnapshotChanged,
                AgentEventKind.RunAccepted,
                AgentEventKind.UserMessageAdmitted,
                AgentEventKind.SessionRunning,
                AgentEventKind.RunRunning,
                AgentEventKind.AssistantMessageCompleted,
                AgentEventKind.RunCompleted,
                AgentEventKind.SessionReady,
            },
            events.Select(e => e.Kind).ToArray());
    }

    [Fact]
    public async Task SendAsync_EmitsDeterministicMonotonicSequencesPerSession()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("one", "two");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");
        var events = new List<AgentEvent>();
        using var subscription = service.Events.Subscribe(events.Add);

        await SendAsync(service, conversationId, target, "one");
        await SendAsync(service, conversationId, target, "two");

        var sequences = events.Select(e => e.Sequence).ToArray();
        Assert.Equal(sequences.OrderBy(s => s).ToArray(), sequences);
        Assert.Equal(sequences.Distinct().Count(), sequences.Length);
        Assert.True(sequences.All(s => s >= 1));
    }

    [Fact]
    public async Task SendAsync_NewSessionPublishesCapabilitySnapshotWithVersionOne()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("hello");
        var conversationId = ConversationId.NewDirect();
        var events = new List<AgentEvent>();
        using var subscription = service.Events.Subscribe(events.Add);

        await SendAsync(service, conversationId, ActorId.PanelSeed("alpha"), "hello");

        var capabilityEvent = Assert.Single(
            events,
            e => e.Kind == AgentEventKind.CapabilitySnapshotChanged);
        var payload = Assert.IsType<AgentCapabilityChangedPayload>(capabilityEvent.Payload);
        Assert.Equal(1, payload.Snapshot.Version);
        Assert.Equal(TestBackendId, payload.Snapshot.BackendId);
    }

    [Fact]
    public async Task CancelAsync_LateCompletionAfterCancellationRequested_CompletesTruthfully()
    {
        var service = CreateService(out var backend);
        backend.SetLateCompletionIgnoringCancellation(
            TimeSpan.FromMilliseconds(200),
            "late winner");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");
        var kinds = new List<AgentEventKind>();
        using var subscription = service.Events.Subscribe(e => kinds.Add(e.Kind));

        var runTask = SendAsync(service, conversationId, target, "race");
        await WaitForRunningSessionAsync(service, conversationId);

        await service.CancelAsync(conversationId);
        var snapshot = await runTask;

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
        Assert.True(kinds.IndexOf(AgentEventKind.RunCancellationRequested) >= 0);
        Assert.True(kinds.IndexOf(AgentEventKind.RunCompleted) > kinds.IndexOf(AgentEventKind.RunCancellationRequested));
    }

    [Fact]
    public async Task EndAsync_DuringCancellationRequested_ClearsOwnership()
    {
        var service = CreateService(out var backend);
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "slow");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");
        var kinds = new List<AgentEventKind>();
        using var subscription = service.Events.Subscribe(e => kinds.Add(e.Kind));

        _ = SendAsync(service, conversationId, target, "slow");
        await WaitForRunningSessionAsync(service, conversationId);
        await service.CancelAsync(conversationId);
        Assert.Contains(AgentEventKind.RunCancellationRequested, kinds);

        await service.EndAsync(conversationId);

        Assert.Null(service.TryGetSessionSnapshot(conversationId));
        Assert.Null(service.TryGetActiveRunSnapshot(conversationId));
        Assert.Contains(AgentEventKind.SessionEnding, kinds);
        Assert.Contains(AgentEventKind.SessionEnded, kinds);
    }

    [Fact]
    public async Task SendAsync_UnexpectedBackendFault_TerminalizesWithIndeterminateClassification()
    {
        var service = CreateService(out var backend);
        backend.SetEnumerationFault("backend exploded");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");
        var events = new List<AgentEvent>();
        using var subscription = service.Events.Subscribe(events.Add);

        var snapshot = await SendAsync(service, conversationId, target, "boom");

        Assert.Equal(AgentRunStatus.Indeterminate, snapshot.Status);
        Assert.Null(service.TryGetActiveRunSnapshot(conversationId));

        var failure = Assert.Single(events, e => e.Kind == AgentEventKind.FailureReported);
        var failurePayload = Assert.IsType<AgentFailurePayload>(failure.Payload);
        Assert.Equal(AgentFailureKind.Indeterminate, failurePayload.FailureKind);

        var terminal = Assert.Single(events, e => e.Kind == AgentEventKind.RunIndeterminate);
        var terminalPayload = Assert.IsType<AgentRunLifecyclePayload>(terminal.Payload);
        Assert.Equal(AgentRunStatus.Indeterminate, terminalPayload.Status);

        backend.SetCompletion("recovered");
        var recovered = await SendAsync(service, conversationId, target, "recovered");
        Assert.Equal(AgentRunStatus.Completed, recovered.Status);
    }

    [Fact]
    public async Task EndAsync_AfterCompletedRunThenRejectedRun_CorrelatesSessionEndWithAdmittedRun()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("first");
        var conversationId = ConversationId.NewDirect();
        var admittedTarget = ActorId.PanelSeed("alpha");
        var events = new List<AgentEvent>();
        using var subscription = service.Events.Subscribe(events.Add);

        var completed = await SendAsync(service, conversationId, admittedTarget, "first");
        var admittedRunId = completed.RunId;

        var rejected = await service.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("wrong-target"),
            TestBackendId,
            ConversationEntryId.New(),
            "rejected");

        Assert.Equal(AgentRunStatus.Rejected, rejected.Status);
        Assert.NotEqual(admittedRunId, rejected.RunId);

        var endEventIndex = events.Count;
        await service.EndAsync(conversationId);

        var ending = events.Skip(endEventIndex).Single(e => e.Kind == AgentEventKind.SessionEnding);
        var ended = events.Skip(endEventIndex).Single(e => e.Kind == AgentEventKind.SessionEnded);
        Assert.Equal(admittedRunId, ending.RunId);
        Assert.Equal(admittedRunId, ended.RunId);
    }

    [Fact]
    public async Task CancelAsync_EmitsCancellationRequestedBeforeTerminalCancelled()
    {
        var service = CreateService(out var backend);
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "slow");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");
        var kinds = new List<AgentEventKind>();
        using var subscription = service.Events.Subscribe(e => kinds.Add(e.Kind));

        var runTask = SendAsync(service, conversationId, target, "cancel-me");
        await WaitForRunningSessionAsync(service, conversationId);

        await service.CancelAsync(conversationId);
        var snapshot = await runTask;

        var requestedIndex = kinds.IndexOf(AgentEventKind.RunCancellationRequested);
        var cancelledIndex = kinds.IndexOf(AgentEventKind.RunCancelled);
        Assert.True(requestedIndex >= 0);
        Assert.True(cancelledIndex > requestedIndex);
        Assert.Equal(AgentRunStatus.Cancelled, snapshot.Status);
    }

    [Fact]
    public async Task SendAsync_CompletesWithCompletedTerminalState()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("done");
        var conversationId = ConversationId.NewDirect();

        var snapshot = await SendAsync(service, conversationId, ActorId.PanelSeed("alpha"), "done");

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
    }

    [Fact]
    public async Task SendAsync_FailureObservationCompletesWithFailedTerminalState()
    {
        var service = CreateService(out var backend);
        backend.SetFailure(AgentFailureKind.Execution, "execution failed");
        var conversationId = ConversationId.NewDirect();

        var snapshot = await SendAsync(service, conversationId, ActorId.PanelSeed("alpha"), "fail");

        Assert.Equal(AgentRunStatus.Failed, snapshot.Status);
    }

    [Fact]
    public async Task EndAsync_ClearsLiveOwnershipWithoutAlteringConversationHistory()
    {
        var store = new ConversationStore();
        var conversation = store.CreateDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"));
        var conversationId = conversation.Id;
        var entryCountBefore = conversation.Entries.Count;

        var service = CreateService(out var backend);
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(5), "slow");
        var target = ActorId.PanelSeed("alpha");

        var sendTask = SendAsync(service, conversationId, target, "hello");
        await WaitForRunningSessionAsync(service, conversationId);

        await service.EndAsync(conversationId);

        Assert.Null(service.TryGetSessionSnapshot(conversationId));
        Assert.Null(service.TryGetActiveRunSnapshot(conversationId));
        Assert.Equal(entryCountBefore, store.TryGet(conversationId, out var unchanged)
            ? unchanged.Entries.Count
            : -1);
    }

    [Fact]
    public async Task EndAsync_OnReadySessionAfterTerminalRun_RemovesSession()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("done");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");

        await SendAsync(service, conversationId, target, "done");
        Assert.NotNull(service.TryGetSessionSnapshot(conversationId));

        await service.EndAsync(conversationId);

        Assert.Null(service.TryGetSessionSnapshot(conversationId));
    }

    [Fact]
    public async Task SendAsync_AfterEnd_CreatesNewSessionWithoutResumeBehavior()
    {
        var service = CreateService(out var backend);
        backend.SetCompletion("one", "two");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");

        await SendAsync(service, conversationId, target, "one");
        var firstSessionId = service.TryGetSessionSnapshot(conversationId)!.SessionId;

        await service.EndAsync(conversationId);
        Assert.Null(service.TryGetSessionSnapshot(conversationId));

        await SendAsync(service, conversationId, target, "two");
        var secondSessionId = service.TryGetSessionSnapshot(conversationId)!.SessionId;

        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Fact]
    public async Task TryGetSessionSnapshot_DuringConcurrentOperations_ReturnsCoherentSnapshots()
    {
        var service = CreateService(out var backend);
        backend.SetDelayedCompletion(TimeSpan.FromMilliseconds(300), "slow");
        var conversationId = ConversationId.NewDirect();
        var target = ActorId.PanelSeed("alpha");

        var sendTask = SendAsync(service, conversationId, target, "slow");
        await WaitForRunningSessionAsync(service, conversationId);

        AgentSessionSnapshot? runningSnapshot = null;
        AgentRunSnapshot? activeRunSnapshot = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            runningSnapshot = service.TryGetSessionSnapshot(conversationId);
            activeRunSnapshot = service.TryGetActiveRunSnapshot(conversationId);
            if (runningSnapshot?.Status == AgentSessionStatus.Running && activeRunSnapshot is not null)
            {
                break;
            }

            await Task.Delay(10);
        }

        Assert.NotNull(runningSnapshot);
        Assert.Equal(AgentSessionStatus.Running, runningSnapshot!.Status);
        Assert.Equal(runningSnapshot.ActiveRunId, activeRunSnapshot!.RunId);
        Assert.Equal(AgentRunStatus.Running, activeRunSnapshot.Status);

        await sendTask;
    }

    [Fact]
    public async Task SendAsync_UnregisteredBackend_ReturnsStructuredRejection()
    {
        var service = CreateService(out _, registerBackend: false);
        var conversationId = ConversationId.NewDirect();

        var snapshot = await service.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            TestBackendId,
            ConversationEntryId.New(),
            "hello");

        Assert.Equal(AgentRunStatus.Rejected, snapshot.Status);
    }

    [Fact]
    public void AgentSessionStateMachine_RejectsInvalidTransition_Regression()
    {
        var machine = new AgentSessionStateMachine(AgentSessionStatus.Ended);

        Assert.Throws<InvalidOperationException>(() =>
            machine.TransitionTo(AgentSessionStatus.Running));
    }

    [Fact]
    public void AgentRunStateMachine_RejectsInvalidTransition_Regression()
    {
        var machine = new AgentRunStateMachine(AgentRunStatus.Created);

        Assert.Throws<InvalidOperationException>(() =>
            machine.TransitionTo(AgentRunStatus.Completed));
    }

    private static AgentSessionService CreateService(
        out FakeAgentBackend backend,
        bool registerBackend = true)
    {
        backend = new FakeAgentBackend(TestBackendId);
        var backends = registerBackend
            ? new IAgentBackend[] { backend }
            : Array.Empty<IAgentBackend>();
        return new AgentSessionService(backends, new AgentEventStream());
    }

    private static Task<AgentRunSnapshot> SendAsync(
        IAgentSessionService service,
        ConversationId conversationId,
        ActorId targetActorId,
        string message) =>
        service.SendAsync(
            conversationId,
            ActorId.HumanUser,
            targetActorId,
            TestBackendId,
            ConversationEntryId.New(),
            message);

    private static async Task WaitForRunningSessionAsync(
        IAgentSessionService service,
        ConversationId conversationId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var session = service.TryGetSessionSnapshot(conversationId);
            var run = service.TryGetActiveRunSnapshot(conversationId);
            if (session?.Status == AgentSessionStatus.Running
                && run?.Status == AgentRunStatus.Running)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Timed out waiting for running session and run.");
    }

    private static async Task WaitForEventKindAsync(
        IAgentSessionService service,
        AgentEventKind kind)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = service.Events
            .Where(e => e.Kind == kind)
            .Take(1)
            .Subscribe(_ => tcs.TrySetResult());

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class FakeAgentBackend : IAgentBackend
    {
        private readonly Queue<FakeBackendPlan> _plans = new();

        public FakeAgentBackend(AgentBackendId backendId)
        {
            BackendId = backendId;
            BackendVersion = "test-1.0.0";
            CapabilitySnapshot = AgentCapabilitySnapshot.CreateInitial(
                backendId,
                new[]
                {
                    AgentCapabilityRow.Create(
                        AgentCapabilityId.MessageCompletion,
                        AgentCapabilityState.Create(
                            advertised: AgentCapabilityFactValue.Supported,
                            available: AgentCapabilityFactValue.Supported,
                            configured: AgentCapabilityFactValue.Supported,
                            permitted: AgentCapabilityFactValue.Unknown,
                            degraded: AgentCapabilityFactValue.NotSupported,
                            currentlyUsable: AgentCapabilityFactValue.Supported)),
                });
        }

        public int ExecuteCallCount { get; private set; }

        public AgentBackendId BackendId { get; }

        public string BackendVersion { get; }

        public AgentCapabilitySnapshot CapabilitySnapshot { get; }

        public void SetCompletion(params string[] assistantTexts)
        {
            _plans.Clear();
            foreach (var text in assistantTexts)
            {
                _plans.Enqueue(FakeBackendPlan.Completion(text));
            }
        }

        public void SetDelayedCompletion(TimeSpan delay, string assistantText)
        {
            _plans.Clear();
            _plans.Enqueue(FakeBackendPlan.DelayedCompletion(delay, assistantText));
        }

        public void SetFailure(AgentFailureKind failureKind, string reason)
        {
            _plans.Clear();
            _plans.Enqueue(FakeBackendPlan.Failure(failureKind, reason));
        }

        public void SetEnumerationFault(string message)
        {
            _plans.Clear();
            _plans.Enqueue(FakeBackendPlan.EnumerationFault(message));
        }

        public void SetLateCompletionIgnoringCancellation(TimeSpan delay, string assistantText)
        {
            _plans.Clear();
            _plans.Enqueue(FakeBackendPlan.LateCompletionIgnoringCancellation(delay, assistantText));
        }

        public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
            AgentBackendRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ExecuteCallCount++;
            if (_plans.Count == 0)
            {
                throw new InvalidOperationException("No fake backend plan configured.");
            }

            var plan = _plans.Dequeue();
            if (plan.EnumerationFaultMessage is { } faultMessage)
            {
                throw new InvalidOperationException(faultMessage);
            }

            if (plan.Delay is { } delay)
            {
                if (plan.IgnoreCancellation)
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!plan.IgnoreCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (plan.FailureKind is { } failureKind)
            {
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    DateTimeOffset.UtcNow,
                    new AgentBackendFailurePayload(failureKind, plan.FailureReason!));
                yield break;
            }

            yield return new AgentBackendEvent(
                AgentBackendEventKind.MessageCompleted,
                DateTimeOffset.UtcNow,
                new AgentBackendMessageCompletedPayload(plan.AssistantText!));
        }

        private sealed record FakeBackendPlan(
            TimeSpan? Delay,
            string? AssistantText,
            AgentFailureKind? FailureKind,
            string? FailureReason,
            string? EnumerationFaultMessage,
            bool IgnoreCancellation)
        {
            public static FakeBackendPlan Completion(string text) =>
                new(null, text, null, null, null, false);

            public static FakeBackendPlan DelayedCompletion(TimeSpan delay, string text) =>
                new(delay, text, null, null, null, false);

            public static FakeBackendPlan Failure(AgentFailureKind kind, string reason) =>
                new(null, null, kind, reason, null, false);

            public static FakeBackendPlan EnumerationFault(string message) =>
                new(null, null, null, null, message, false);

            public static FakeBackendPlan LateCompletionIgnoringCancellation(TimeSpan delay, string text) =>
                new(delay, text, null, null, null, true);
        }
    }
}
