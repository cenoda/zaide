using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class Phase17ActionContractsBrokerTests
{
    [Fact]
    public async Task UnavailableAgentActionBroker_ReturnsBrokerUnavailable()
    {
        var broker = new UnavailableAgentActionBroker();
        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("README.md")),
            correlationKey: null,
            cancellationToken: default);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.BrokerUnavailable, result.FailureKind);
        Assert.True(result.IsTerminal);
    }

    [Fact]
    public async Task ContractAgentActionBroker_RejectsConcurrentRequestsForOneRun()
    {
        var runSlot = new AgentActionRunSlotTracker();
        runSlot.TryReserve(AgentActionId.New());

        var broker = CreateBroker(runSlot, new AgentActionCorrelationRegistry());
        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("README.md")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionFailureKind.ConcurrentActionRejected, result.FailureKind);
    }

    [Fact]
    public async Task ContractAgentActionBroker_ReplaysDuplicateCorrelationKey()
    {
        var registry = new AgentActionCorrelationRegistry();
        var broker = CreateBroker(new AgentActionRunSlotTracker(), registry);
        const string correlationKey = "duplicate-1";
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("new.txt"),
            "hello");

        var first = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);
        var second = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.DuplicateReplay, second.ResultKind);
        Assert.Equal(first.Summary, second.Summary);
    }

    [Fact]
    public async Task ContractAgentActionBroker_ReturnsRevokedWhenDisposed()
    {
        var broker = CreateBroker(new AgentActionRunSlotTracker(), new AgentActionCorrelationRegistry());
        broker.Revoke();

        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("README.md")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionFailureKind.BrokerRevoked, result.FailureKind);
    }

    [Fact]
    public async Task ContractAgentActionBroker_ParallelUnrelatedRequests_RejectAllButOne()
    {
        var runSlot = new AgentActionRunSlotTracker();
        var broker = CreateBroker(runSlot, new AgentActionCorrelationRegistry());
        var payload = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("README.md"));
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey: null, CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));
        Assert.True(runSlot.HasActiveAction);

        var secondResult = await broker.RequestAsync(payload, correlationKey: null, CancellationToken.None);
        allowProcessingToComplete.Set();
        var firstResult = await firstRequest;

        Assert.NotEqual(AgentActionFailureKind.ConcurrentActionRejected, firstResult.FailureKind);
        Assert.Equal(AgentActionFailureKind.ConcurrentActionRejected, secondResult.FailureKind);
    }

    [Fact]
    public async Task ContractAgentActionBroker_ParallelSameCorrelationKeySameFingerprint_ReplaysWithoutDuplicateSideEffect()
    {
        var broker = CreateBroker(new AgentActionRunSlotTracker(), new AgentActionCorrelationRegistry());
        const string correlationKey = "parallel-same-fingerprint";
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("new.txt"),
            "hello");
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey, CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));

        var secondRequest = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey, CancellationToken.None));

        allowProcessingToComplete.Set();
        var results = await Task.WhenAll(firstRequest, secondRequest);

        var admitted = results.Where(result => result.ResultKind != AgentActionResultKind.DuplicateReplay).ToArray();
        var replays = results.Where(result => result.ResultKind == AgentActionResultKind.DuplicateReplay).ToArray();

        Assert.Single(admitted);
        Assert.Single(replays);
        Assert.Equal(admitted[0].Summary, replays[0].Summary);
    }

    [Fact]
    public async Task ContractAgentActionBroker_ParallelSameCorrelationKeyDifferentFingerprint_ReturnsCorrelationKeyMismatch()
    {
        var broker = CreateBroker(new AgentActionRunSlotTracker(), new AgentActionCorrelationRegistry());
        const string correlationKey = "parallel-mismatch";
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await broker.RequestAsync(
                new AgentCreateFileActionPayload(
                    AgentWorkspaceRelativePath.Normalize("first.txt"),
                    "one"),
                correlationKey,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));

        var secondResult = await broker.RequestAsync(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("second.txt"),
                "two"),
            correlationKey,
            CancellationToken.None);

        allowProcessingToComplete.Set();
        var firstResult = await firstRequest;

        Assert.Equal(AgentActionFailureKind.CorrelationKeyMismatch, secondResult.FailureKind);
        Assert.NotEqual(AgentActionFailureKind.CorrelationKeyMismatch, firstResult.FailureKind);
    }

    [Fact]
    public async Task ContractAgentActionBroker_CancelledTokenDuringInFlightWait_ReturnsCancelled()
    {
        var runSlot = new AgentActionRunSlotTracker();
        var registry = new AgentActionCorrelationRegistry();
        var broker = CreateBroker(runSlot, registry);
        const string correlationKey = "cancel-wait";
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("cancel.txt"),
            "test");
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        // Start first request to hold the in-flight slot.
        var firstRequest = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey, CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));

        // Second request with cancellable token enters the in-flight wait.
        // We cancel the token after a short delay to trigger cancellation
        // during the bounded Monitor.Wait polling loop.
        using var cts = new CancellationTokenSource();
        var secondTask = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey, cts.Token));

        // Give the second request time to enter the wait loop.
        await Task.Delay(200);
        cts.Cancel();

        var secondResult = await secondTask;
        allowProcessingToComplete.Set();
        await firstRequest;

        Assert.True(secondResult.IsTerminal);
        Assert.True(
            secondResult.ResultKind == AgentActionResultKind.Cancelled
            || secondResult.ResultKind == AgentActionResultKind.DuplicateReplay,
            $"Expected Cancelled or DuplicateReplay, got {secondResult.ResultKind}");
    }

    [Fact]
    public async Task ContractAgentActionBroker_RevocationWakesWaitingThreads()
    {
        var runSlot = new AgentActionRunSlotTracker();
        var registry = new AgentActionCorrelationRegistry();
        var broker = CreateBroker(runSlot, registry);
        const string correlationKey = "revoke-wait";
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("revoke.txt"),
            "test");
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        // Start first request to hold the in-flight slot.
        var firstRequest = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey, CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));

        // Second request will enter the in-flight wait path.
        var secondTask = Task.Run(async () =>
            await broker.RequestAsync(payload, correlationKey, CancellationToken.None));

        // Give the second request time to enter Monitor.Wait.
        await Task.Delay(200);

        // Revoke the broker — this must wake the waiting thread.
        broker.Revoke();

        var secondResult = await secondTask;
        allowProcessingToComplete.Set();
        await firstRequest;

        Assert.True(
            secondResult.ResultKind == AgentActionResultKind.Denied
            || secondResult.ResultKind == AgentActionResultKind.DuplicateReplay,
            $"Expected Denied or DuplicateReplay, got {secondResult.ResultKind}");
        Assert.True(secondResult.IsTerminal);
    }

    [Fact]
    public async Task AgentActionCorrelationRegistry_RevokeWakesWaiters()
    {
        var registry = new AgentActionCorrelationRegistry();
        var key = AgentActionCorrelationKey.FromValue("tool-call-1");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("kind=CreateFile");

        // Begin an in-flight correlation to block waiters.
        registry.BeginInFlightCorrelation(key, fingerprint);

        var waiterReady = new ManualResetEventSlim(initialState: false);
        bool waitReturned = false;
        var waitTask = Task.Run(() =>
        {
            waiterReady.Set();
            waitReturned = registry.TryWaitForInFlightReplay(
                key, fingerprint, CancellationToken.None, out _);
        });

        Assert.True(waiterReady.Wait(TimeSpan.FromSeconds(1)));

        // Revoke should wake the waiter.
        registry.Revoke();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(waitReturned);
        Assert.True(registry.IsRevoked);
    }

    [Fact]
    public void AgentActionCorrelationRegistry_CancelledTokenReturnsImmediately()
    {
        var registry = new AgentActionCorrelationRegistry();
        var key = AgentActionCorrelationKey.FromValue("tool-call-1");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("kind=CreateFile");

        // Begin an in-flight correlation to simulate a blocked state.
        registry.BeginInFlightCorrelation(key, fingerprint);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var returned = registry.TryWaitForInFlightReplay(
            key, fingerprint, cts.Token, out var replay);

        Assert.False(returned);
        Assert.Null(replay);
    }

    private static ContractAgentActionBroker CreateBroker(
        AgentActionRunSlotTracker runSlot,
        AgentActionCorrelationRegistry correlationRegistry)
    {
        var scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(
            System.IO.Path.GetTempPath());

        return new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            AgentBackendId.FromValue("backend:test"),
            new FakeWorkspaceActionAuthority(scope),
            new CountingAgentFileReader(),
            new CountingAgentFileMutator(),
            new FakeTrustedCommandResolver(),
            runSlot,
            correlationRegistry);
    }
}
