using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Focused tests for <see cref="InteractiveAgentPermissionReviewService"/>:
/// the service must invoke the visible review path through
/// <see cref="IAgentPermissionDialogPresenter"/>, preserve cancellation, and
/// fail closed (PermissionUnavailable) when no review surface exists.
/// </summary>
public sealed class Phase17PermissionReviewServiceTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceActionScope _scope;

    public Phase17PermissionReviewServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zaide-p17-review-" + Guid.NewGuid().ToString("N"));
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

    private AgentActionRequest ComposeRequest(AgentActionPayload payload) =>
        AgentActionRequestComposer.Compose(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            _scope.Identity,
            _scope.Generation,
            new FakeTrustedCommandResolver(),
            payload);

    private sealed class RecordingPresenter : IAgentPermissionDialogPresenter
    {
        private readonly Func<CancellationToken, Task<bool>> _behavior;

        public RecordingPresenter(Func<CancellationToken, Task<bool>> behavior)
        {
            _behavior = behavior;
        }

        public List<(AgentActionRequest Request,
            AgentActionDisplaySummary Summary,
            WorkspaceActionScope? Scope)> Invocations { get; } = new();

        public Task<bool> ShowAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken)
        {
            Invocations.Add((request, displaySummary, workspaceScope));
            return _behavior(cancellationToken);
        }
    }

    [Fact]
    public async Task RequestDecision_InvokesVisibleReviewPath_AndAllowFlowsThrough()
    {
        var presenter = new RecordingPresenter(_ => Task.FromResult(true));
        var service = new InteractiveAgentPermissionReviewService(presenter);
        var request = ComposeRequest(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"));
        var summary = AgentActionDisplaySummaryBuilder.Build(request.Payload);

        var decision = await service.RequestDecisionAsync(
            request, summary, _scope, CancellationToken.None);

        var invocation = Assert.Single(presenter.Invocations);
        Assert.Same(request, invocation.Request);
        Assert.Same(summary, invocation.Summary);
        Assert.Same(_scope, invocation.Scope);

        Assert.True(decision.IsAllow);
        Assert.Equal(AgentPermissionDecisionStatus.Published, decision.Status);
        Assert.Equal(request.Fingerprint, decision.RequestFingerprint);
        Assert.Equal(
            AgentActionPermissionClassification.RequiresUserDecision,
            decision.Classification);
    }

    [Fact]
    public async Task RequestDecision_PresenterDenies_ReturnsDeniedDecision()
    {
        var presenter = new RecordingPresenter(_ => Task.FromResult(false));
        var service = new InteractiveAgentPermissionReviewService(presenter);
        var request = ComposeRequest(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"));

        var decision = await service.RequestDecisionAsync(
            request,
            AgentActionDisplaySummaryBuilder.Build(request.Payload),
            _scope,
            CancellationToken.None);

        Assert.False(decision.IsAllow);
        Assert.Equal(AgentPermissionDecisionStatus.Denied, decision.Status);
    }

    [Fact]
    public async Task RequestDecision_NoPresenter_FailsClosedByThrowing()
    {
        var service = new InteractiveAgentPermissionReviewService();
        var request = ComposeRequest(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RequestDecisionAsync(
                request,
                AgentActionDisplaySummaryBuilder.Build(request.Payload),
                _scope,
                CancellationToken.None));
    }

    [Fact]
    public async Task RequestDecision_CancellationDuringDialog_RethrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var presenter = new RecordingPresenter(async ct =>
        {
            // Simulate an open dialog waiting on the user while the run is
            // cancelled from outside.
            await Task.Delay(Timeout.Infinite, ct);
            return false;
        });
        var service = new InteractiveAgentPermissionReviewService(presenter);
        var request = ComposeRequest(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"));

        var pending = service.RequestDecisionAsync(
            request,
            AgentActionDisplaySummaryBuilder.Build(request.Payload),
            _scope,
            cts.Token).AsTask();

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.Single(presenter.Invocations);
    }

    [Fact]
    public async Task RequestDecision_PresenterFailure_PropagatesForPermissionUnavailable()
    {
        var presenter = new RecordingPresenter(
            _ => Task.FromException<bool>(new InvalidOperationException("review surface crashed")));
        var service = new InteractiveAgentPermissionReviewService(presenter);
        var request = ComposeRequest(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RequestDecisionAsync(
                request,
                AgentActionDisplaySummaryBuilder.Build(request.Payload),
                _scope,
                CancellationToken.None));
    }

    [Fact]
    public async Task ProductionPresenter_WithoutOwnerWindow_FailsClosedByThrowing()
    {
        var presenter = new PermissionReviewDialogPresenter();
        var request = ComposeRequest(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await presenter.ShowAsync(
                request,
                AgentActionDisplaySummaryBuilder.Build(request.Payload),
                _scope,
                CancellationToken.None));
    }

    // ----------------------------------------------------------------
    // Broker integration through the real interactive service
    // ----------------------------------------------------------------

    private ContractAgentActionBroker CreateBroker(IAgentPermissionReviewService reviewService) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            new FakeWorkspaceActionAuthority(_scope),
            new CountingAgentFileReader(),
            new FakeTrustedCommandResolver(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            reviewService);

    [Fact]
    public async Task Broker_UiAbsence_FailsClosedAsPermissionUnavailable()
    {
        var broker = CreateBroker(new InteractiveAgentPermissionReviewService());

        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionUnavailable, result.FailureKind);
    }

    [Fact]
    public async Task Broker_CancellationDuringDialog_ReturnsCancelledNotPermissionDenied()
    {
        using var cts = new CancellationTokenSource();
        var dialogOpened = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var presenter = new RecordingPresenter(async ct =>
        {
            dialogOpened.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return false;
        });
        var broker = CreateBroker(new InteractiveAgentPermissionReviewService(presenter));

        var pending = broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            cts.Token).AsTask();

        await dialogOpened.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();

        var result = await pending;

        Assert.Equal(AgentActionResultKind.Cancelled, result.ResultKind);
        Assert.NotEqual(AgentActionFailureKind.PermissionDenied, result.FailureKind);
    }

    [Fact]
    public async Task Broker_UserAllowThroughVisibleReviewPath_Succeeds()
    {
        var presenter = new RecordingPresenter(_ => Task.FromResult(true));
        var broker = CreateBroker(new InteractiveAgentPermissionReviewService(presenter));

        var result = await broker.RequestAsync(
            new AgentCreateFileActionPayload(AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        var invocation = Assert.Single(presenter.Invocations);
        Assert.Same(_scope, invocation.Scope);
    }
}
