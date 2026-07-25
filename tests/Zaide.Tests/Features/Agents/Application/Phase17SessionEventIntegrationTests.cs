using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M8 — session/event integration through the fake action requester,
/// authoritative run binding, audit snapshots, projection ownership, and
/// revocation propagation.
/// </summary>
public sealed class Phase17SessionEventIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceActionScope _scope;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;
    private readonly AgentActionAuditStore _auditStore;
    private readonly AgentEventStream _eventStream;
    private readonly List<AgentEvent> _capturedEvents = new();
    private readonly FakeActionRequesterBackend _backend;
    private readonly AgentSessionService _session;
    private readonly ConversationStore _conversationStore;

    public Phase17SessionEventIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zaide-p17-m8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note.txt"), "hello");

        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_root);
        _workspaceAuthority = new FakeWorkspaceActionAuthority(_scope);
        _auditStore = new AgentActionAuditStore();
        _eventStream = new AgentEventStream();
        _eventStream.Events.Subscribe(_capturedEvents.Add);

        var brokerFactory = new AgentActionBrokerFactory(
            _workspaceAuthority,
            new WorkspaceFileReader(),
            new WorkspaceFileMutator(),
            new DefaultAgentCommandResolver(),
            new WorkspaceCommandExecutor(),
            new AllowingPermissionReviewService(),
            NullAgentDocumentReconciler.Instance);

        _backend = new FakeActionRequesterBackend();
        _session = new AgentSessionService(
            new IAgentBackend[] { _backend },
            _eventStream,
            brokerFactory,
            _auditStore,
            _workspaceAuthority);

        _conversationStore = Conversations.ConversationsTestSupport.CreateStore();
        _ = new AgentConversationEventProjection(_eventStream.Events, _conversationStore, Conversations.ConversationsTestSupport.CreateCatalog());
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task FakeRequester_ReadAction_EmitsOrderedFactsAndProjectsSummary()
    {
        _backend.SetReadAndComplete("note.txt", correlationKey: "read-1");
        var conversation = _conversationStore.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var conversationId = conversation.Id;

        var snapshot = await _session.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            _backend.BackendId,
            ConversationEntryId.New(),
            "read file",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);

        var actionEvents = _capturedEvents
            .Where(e => e.Kind is AgentEventKind.ActionRequested
                or AgentEventKind.ActionPermissionClassified
                or AgentEventKind.ActionExecutionStarted
                or AgentEventKind.ActionResultReported)
            .ToArray();
        Assert.True(actionEvents.Length >= 4);
        Assert.True(IsMonotonic(actionEvents.Select(e => e.Sequence)));

        var resultEvent = _capturedEvents.Single(e => e.Kind == AgentEventKind.ActionResultReported);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideExecuted, resultEvent.EvidenceLevel);
        Assert.IsType<AgentActionFactPayload>(resultEvent.Payload);

        var audit = _auditStore.GetRunSnapshot(snapshot.RunId, maxRecords: 32);
        Assert.NotEmpty(audit);
        Assert.All(audit, record => Assert.Equal(snapshot.RunId, record.RunId));

        Assert.True(_conversationStore.TryGet(conversationId, out conversation));
        Assert.Contains(
            conversation.Entries,
            entry => entry.Kind == ConversationEntryKind.SystemNotification
                     && entry.Content.Contains("Read file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateCorrelationKey_ReplaysWithoutDuplicateExecutionOrFacts()
    {
        _backend.SetDelayedAction(
            TimeSpan.Zero,
            async (broker, token) =>
            {
                var payload = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt"));
                var first = await broker.RequestAsync(payload, "dup-key", token);
                var second = await broker.RequestAsync(payload, "dup-key", token);
                Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
                Assert.Equal(AgentActionResultKind.DuplicateReplay, second.ResultKind);
                return first;
            });

        var conversation = _conversationStore.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var conversationId = conversation.Id;

        await _session.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            _backend.BackendId,
            ConversationEntryId.New(),
            "dup",
            CancellationToken.None);

        var resultFacts = _capturedEvents
            .Where(e => e.Kind == AgentEventKind.ActionResultReported)
            .ToArray();
        Assert.Single(resultFacts);
    }

    [Fact]
    public async Task RunCancellation_RevokesPendingActionAuthority()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockingReview = new BlockingPermissionReviewService(entered);
        var brokerFactory = new AgentActionBrokerFactory(
            _workspaceAuthority,
            new WorkspaceFileReader(),
            new WorkspaceFileMutator(),
            new DefaultAgentCommandResolver(),
            new WorkspaceCommandExecutor(),
            blockingReview,
            NullAgentDocumentReconciler.Instance);

        var stream = new AgentEventStream();
        var captured = new List<AgentEvent>();
        stream.Events.Subscribe(captured.Add);
        var backend = new FakeActionRequesterBackend();
        backend.SetDelayedAction(
            TimeSpan.Zero,
            (broker, token) => broker.RequestAsync(
                new AgentCreateFileActionPayload(
                    AgentWorkspaceRelativePath.Normalize("new.txt"),
                    "created"),
                correlationKey: null,
                token),
            assistantText: "late");

        var session = new AgentSessionService(
            new IAgentBackend[] { backend },
            stream,
            brokerFactory,
            _auditStore,
            _workspaceAuthority);

        var conversation = _conversationStore.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var conversationId = conversation.Id;

        var sendTask = session.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            backend.BackendId,
            ConversationEntryId.New(),
            "cancel",
            CancellationToken.None);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.CancelAsync(conversationId, CancellationToken.None);
        var snapshot = await sendTask;

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
        Assert.Contains(captured, e => e.Kind == AgentEventKind.RunCancellationRequested);
        Assert.Contains(
            captured,
            e => e.Kind == AgentEventKind.ActionResultReported
                 && e.Payload is AgentActionFactPayload fact
                 && fact.ResultKind == AgentActionResultKind.Cancelled);
        session.Dispose();
    }

    [Fact]
    public async Task WorkspaceInvalidation_RevokesActiveRunBroker()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.SetDelayedAction(
            TimeSpan.Zero,
            async (broker, token) =>
            {
                gate.SetResult();
                return await broker.RequestAsync(
                    new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
                    correlationKey: null,
                    token);
            },
            assistantText: "late");

        var conversation = _conversationStore.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var conversationId = conversation.Id;

        var sendTask = _session.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            _backend.BackendId,
            ConversationEntryId.New(),
            "invalidate",
            CancellationToken.None);

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _workspaceAuthority.RaiseScopeInvalidated();
        var snapshot = await sendTask;

        Assert.Contains(
            _capturedEvents,
            e => e.Kind is AgentEventKind.ActionRevoked or AgentEventKind.ActionResultReported);
        Assert.NotEqual(AgentRunStatus.Running, snapshot.Status);
    }

    [Fact]
    public void LegacyBackendCapabilityRows_RemainUnavailable()
    {
        var tools = _backend.CapabilitySnapshot.Rows.Single(row => row.CapabilityId == AgentCapabilityId.Tools);
        var permissions = _backend.CapabilitySnapshot.Rows.Single(row => row.CapabilityId == AgentCapabilityId.Permissions);

        Assert.Equal(AgentCapabilityFactValue.Unavailable, tools.State.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, permissions.State.Advertised);
    }

    [Fact]
    public void AuditSnapshot_IsBoundedForCurrentLifetime()
    {
        var bounded = _auditStore.GetCurrentLifetimeSnapshot(maxRecords: 4);
        Assert.True(bounded.Count <= 4);
    }

    [Fact]
    public async Task LegacyBackendSession_StillReceivesUnavailableBroker()
    {
        var legacyBackend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        legacyBackend.SetCompletion("ok");
        var stream = new AgentEventStream();
        var captured = new List<AgentEvent>();
        stream.Events.Subscribe(captured.Add);
        var session = new AgentSessionService(new IAgentBackend[] { legacyBackend }, stream);

        var conversationId = ConversationId.NewDirect();
        var snapshot = await session.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            legacyBackend.BackendId,
            ConversationEntryId.New(),
            "hello",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
        Assert.DoesNotContain(captured, e => e.Kind == AgentEventKind.ActionRequested);
    }

    private static bool IsMonotonic(IEnumerable<long> sequence) =>
        sequence.Zip(sequence.Skip(1), (left, right) => right > left).All(result => result);

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                isAllow: true));
        }
    }

    private sealed class BlockingPermissionReviewService : IAgentPermissionReviewService
    {
        private readonly TaskCompletionSource _entered;

        public BlockingPermissionReviewService(TaskCompletionSource entered)
        {
            _entered = entered;
        }

        public async ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Permission review should have been cancelled.");
        }
    }
}
