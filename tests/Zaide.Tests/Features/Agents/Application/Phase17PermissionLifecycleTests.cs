using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class Phase17PermissionLifecycleTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceActionScope _scope;

    public Phase17PermissionLifecycleTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zaide-p17-perm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_root);
    }

    public void Dispose()
    {
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

    private ContractAgentActionBroker CreateBroker(
        IAgentPermissionReviewService reviewService,
        IWorkspaceActionAuthority? authority = null,
        ActorId? initiatingActor = null,
        ActorId? targetActor = null,
        AgentBackendId? backendId = null,
        AgentActionRunSlotTracker? runSlot = null,
        AgentActionCorrelationRegistry? correlationRegistry = null)
    {
        var auth = authority ?? new FakeWorkspaceActionAuthority(_scope);

        return new ContractAgentActionBroker(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            initiatingActor ?? ActorId.HumanUser,
            targetActor ?? ActorId.PanelSeed("agent-target"),
            backendId ?? AgentBackendId.FromValue("backend:test"),
            auth,
            new CountingAgentFileReader(),
            new FakeTrustedCommandResolver(),
            runSlot ?? new AgentActionRunSlotTracker(),
            correlationRegistry ?? new AgentActionCorrelationRegistry(),
            reviewService);
    }

    private sealed class StubPermissionReviewService(
        Func<AgentActionRequest, AgentActionDisplaySummary, WorkspaceActionScope?, CancellationToken, ValueTask<AgentPermissionDecision>> handler)
        : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            handler(request, displaySummary, workspaceScope, cancellationToken);
    }

    [Fact]
    public async Task ReadRequest_AutoAllowedWithoutPrompt()
    {
        var prompted = false;
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            prompted = true;
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.AllowedByLockedPolicy,
                AgentPermissionDecisionStatus.Consumed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("README.md")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.False(prompted, "Read requests should be auto-allowed by policy without prompting permission review.");
    }

    [Fact]
    public async Task NonReadRequest_RequiresUserDecision_Allowed()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
    }

    [Fact]
    public async Task NonReadRequest_DeniedByUser()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Denied,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                false));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task MismatchedFingerprint_Rejected()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            var bogusFingerprint = AgentActionRequestFingerprint.FromCanonicalText("bogus");

            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                bogusFingerprint,
                AgentActionPermissionClassification.AllowedByLockedPolicy,
                AgentPermissionDecisionStatus.Consumed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task ExpiredDecision_Rejected()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow.AddHours(-2),
                DateTimeOffset.UtcNow.AddHours(-1),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionExpired, result.FailureKind);
    }

    [Fact]
    public async Task StaleWorkspace_Revokes()
    {
        var authority = new FakeWorkspaceActionAuthority(_scope);
        var reviewService = new StubPermissionReviewService(async (req, sum, ws, ct) =>
        {
            authority.IsStale = true;
            await Task.Yield();
            return new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true);
        });

        var broker = CreateBroker(reviewService, authority);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleWorkspace, result.FailureKind);
    }

    [Fact]
    public async Task BackendSelfApproval_Rejected()
    {
        var actor = ActorId.FromValue("agent-1");
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.AllowedByLockedPolicy,
                AgentPermissionDecisionStatus.Consumed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService, initiatingActor: actor, targetActor: actor);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PolicyDenied, result.FailureKind);
    }

    [Fact]
    public async Task ObserverFailure_FailsClosed()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            throw new InvalidOperationException("UI failure");
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionUnavailable, result.FailureKind);
    }

    [Fact]
    public async Task DisplaySummary_ContainsAllRequiredFields()
    {
        var payload = new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("test.txt"), "data");
        var summary = AgentActionDisplaySummaryBuilder.Build(payload);

        Assert.Equal(AgentActionKind.CreateFile, summary.Kind);
        Assert.Contains("test.txt", summary.DetailText, StringComparison.Ordinal);
        Assert.Contains("create", summary.DetailText, StringComparison.Ordinal);
        Assert.Contains("Scope: this exact request only.", summary.DetailText, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Corrective tests: cancellation, forged status/classification
    // ----------------------------------------------------------------

    [Fact]
    public async Task CancellationDuringReview_ReturnsCancelled()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            throw new OperationCanceledException(ct);
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Cancelled, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.Indeterminate, result.FailureKind);
    }

    [Fact]
    public async Task ForgedStatus_Consumed_Rejected()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Consumed, // Forged: should be Published
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task ForgedStatus_Revoked_Rejected()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Revoked, // Forged
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task ForgedStatus_Expired_RejectedEvenIfAllowTrue()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Expired, // Forged
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true)); // IsAllow=true but status is Expired
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task ForgedClassification_WrongClassification_Rejected()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.AllowedByLockedPolicy, // Forged classification
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task CorrectLifecycle_DeniedStatus_ReturnsPermissionDenied()
    {
        // Prove the service correctly returns Denied status for user-dismissed decisions.
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Denied,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                false));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task ForgedStatus_Denied_RejectedEvenIfAllowTrue()
    {
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Denied, // Forged: Denied status with IsAllow=true
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    // ----------------------------------------------------------------
    // Corrective tests: atomic Published → Consumed lifecycle
    // ----------------------------------------------------------------

    private static AgentPermissionDecision CreateDecision(
        AgentPermissionDecisionStatus status,
        bool isAllow = true) =>
        new(
            AgentPermissionDecisionId.New(),
            AgentActionRequestFingerprint.FromCanonicalText("kind=CreateFile"),
            AgentActionPermissionClassification.RequiresUserDecision,
            status,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            isAllow);

    [Fact]
    public void TryConsume_TransitionsPublishedToConsumed_ExactlyOnce()
    {
        var decision = CreateDecision(AgentPermissionDecisionStatus.Published);

        Assert.True(decision.TryConsume());
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, decision.Status);

        // Second consumption must fail: one decision authorizes at most once.
        Assert.False(decision.TryConsume());
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, decision.Status);
    }

    [Fact]
    public void TryConsume_RejectsNonPublishedStatuses()
    {
        var nonPublishedStatuses = new[]
        {
            AgentPermissionDecisionStatus.Consumed,
            AgentPermissionDecisionStatus.Denied,
            AgentPermissionDecisionStatus.Revoked,
            AgentPermissionDecisionStatus.Expired,
        };

        foreach (var status in nonPublishedStatuses)
        {
            var decision = CreateDecision(status);

            Assert.False(decision.TryConsume());
            Assert.Equal(status, decision.Status);
        }
    }

    [Fact]
    public async Task TryConsume_ConcurrentRacers_ExactlyOneWins()
    {
        var decision = CreateDecision(AgentPermissionDecisionStatus.Published);
        using var startGate = new ManualResetEventSlim(initialState: false);

        var racers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                startGate.Wait();
                return decision.TryConsume();
            }))
            .ToArray();

        startGate.Set();
        var outcomes = await Task.WhenAll(racers);

        Assert.Equal(1, outcomes.Count(consumed => consumed));
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, decision.Status);
    }

    [Fact]
    public async Task AllowedDecision_IsConsumedAfterAuthorization()
    {
        AgentPermissionDecision? issuedDecision = null;
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            issuedDecision = new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true);
            return ValueTask.FromResult(issuedDecision);
        });

        var broker = CreateBroker(reviewService);
        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.NotNull(issuedDecision);
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, issuedDecision!.Status);
    }

    [Fact]
    public async Task ReplayedConsumedDecision_CannotAuthorizeSecondRequest()
    {
        // Adversarial: a review service replays the same (already consumed)
        // decision object for a second identical request. The broker must
        // reject it because the status is no longer Published.
        AgentPermissionDecision? retainedDecision = null;
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            retainedDecision ??= new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true);
            return ValueTask.FromResult(retainedDecision);
        });

        var broker = CreateBroker(reviewService);
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("new.txt"), "content");

        var first = await broker.RequestAsync(payload, correlationKey: null, CancellationToken.None);
        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, retainedDecision!.Status);

        var second = await broker.RequestAsync(payload, correlationKey: null, CancellationToken.None);
        Assert.Equal(AgentActionResultKind.Denied, second.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionDenied, second.FailureKind);
    }

    [Fact]
    public async Task WorkspaceScope_PassedToReviewService()
    {
        WorkspaceActionScope? receivedScope = null;
        var reviewService = new StubPermissionReviewService((req, sum, ws, ct) =>
        {
            receivedScope = ws;
            return ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                req.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Denied,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                false));
        });

        var broker = CreateBroker(reviewService);
        await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.NotNull(receivedScope);
        Assert.Equal(_scope.RootPath, receivedScope!.RootPath);
        Assert.Equal(_scope.CapturedCanonicalRoot, receivedScope.CapturedCanonicalRoot);
    }
}
