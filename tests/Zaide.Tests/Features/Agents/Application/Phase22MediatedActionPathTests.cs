using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Editor.Application;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents.Acp.Backend;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class Phase22MediatedActionPathTests
{
    [Fact]
    public async Task NativeHarness_Read_SucceedsThroughPhase17Broker()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        var relativePath = "docs/readme.md";
        Directory.CreateDirectory(Path.Combine(harness.WorkspaceRoot, "docs"));
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, relativePath), "native read");

        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReadFileToolName,
                """{"path":"docs/readme.md"}"""),
            Phase22MediatedActionTestSupport.Complete());

        var resultFact = harness.GetSingleResultFact();
        Assert.NotNull(resultFact);
        Assert.Equal(AgentActionResultKind.Succeeded, resultFact!.ResultKind);
        Assert.Equal(AgentActionKind.ReadFile, resultFact.ActionKind);
    }

    [Fact]
    public async Task NativeHarness_Create_SucceedsThroughPhase17Broker()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.CreateFileToolName,
                """{"path":"created.txt","content":"created by harness"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.True(File.Exists(Path.Combine(harness.WorkspaceRoot, "created.txt")));
        Assert.Equal("created by harness", File.ReadAllText(Path.Combine(harness.WorkspaceRoot, "created.txt")));
        Assert.Equal(AgentActionResultKind.Succeeded, harness.GetSingleResultFact()!.ResultKind);
    }

    [Fact]
    public async Task NativeHarness_Replace_SucceedsThroughPhase17Broker()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        var relativePath = "replace.txt";
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, relativePath), "original");
        var revision = AgentContentRevision.FromUtf8Text("original");

        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReplaceFileToolName,
                $$"""{"path":"replace.txt","base_revision":"{{revision.Value}}","content":"replacement"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.Equal("replacement", File.ReadAllText(Path.Combine(harness.WorkspaceRoot, relativePath)));
        Assert.Equal(AgentActionResultKind.Succeeded, harness.GetSingleResultFact()!.ResultKind);
    }

    [Fact]
    public async Task Acp_Read_SucceedsThroughPhase17Broker()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.Acp);
        var relativePath = "docs/acp-read.md";
        Directory.CreateDirectory(Path.Combine(harness.WorkspaceRoot, "docs"));
        var absolutePath = Path.Combine(harness.WorkspaceRoot, relativePath);
        File.WriteAllText(absolutePath, "acp read");

        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpReadRequest(absolutePath),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        Assert.Equal(AgentActionResultKind.Succeeded, harness.GetSingleResultFact()!.ResultKind);
        Assert.Equal(AgentActionKind.ReadFile, harness.GetSingleResultFact()!.ActionKind);
    }

    [Fact]
    public async Task Acp_Create_SucceedsThroughPhase17Broker()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.Acp);
        var absolutePath = Path.Combine(harness.WorkspaceRoot, "acp-created.txt");
        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpWriteRequest(absolutePath, "created by acp"),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        Assert.True(File.Exists(absolutePath));
        Assert.Equal("created by acp", File.ReadAllText(absolutePath));
        Assert.Equal(AgentActionResultKind.Succeeded, harness.GetResultFact(AgentActionKind.CreateFile)!.ResultKind);
    }

    [Fact]
    public async Task Acp_Replace_SucceedsThroughPhase17Broker()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.Acp);
        var absolutePath = Path.Combine(harness.WorkspaceRoot, "acp-replace.txt");
        File.WriteAllText(absolutePath, "original");
        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpWriteRequest(absolutePath, "acp replacement"),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        Assert.Equal("acp replacement", File.ReadAllText(absolutePath));
        Assert.Equal(AgentActionResultKind.Succeeded, harness.GetResultFact(AgentActionKind.ReplaceFile)!.ResultKind);
    }

    [Fact]
    public async Task Permission_Allow_ExecutesOnlyAfterFinalAuthorization()
    {
        var review = new CapturingAllowingPermissionReviewService();
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness, review);
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.CreateFileToolName,
                """{"path":"allowed.txt","content":"allowed"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.NotNull(review.Decision);
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, review.Decision!.Status);
        Assert.True(File.Exists(Path.Combine(harness.WorkspaceRoot, "allowed.txt")));
    }

    [Fact]
    public async Task Permission_Deny_DoesNotExecuteOrMutateWorkspace()
    {
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            new DenyingPermissionReviewService());
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.CreateFileToolName,
                """{"path":"denied.txt","content":"denied"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.False(File.Exists(Path.Combine(harness.WorkspaceRoot, "denied.txt")));
        Assert.Equal(AgentActionResultKind.Denied, harness.GetSingleResultFact()!.ResultKind);
    }

    [Fact]
    public async Task Permission_Dismiss_DoesNotExecuteOrMutateWorkspace()
    {
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.Acp,
            new DismissingPermissionReviewService());
        var absolutePath = Path.Combine(harness.WorkspaceRoot, "dismissed.txt");
        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpWriteRequest(absolutePath, "dismissed"),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        Assert.False(File.Exists(absolutePath));
        Assert.Equal(AgentActionResultKind.Denied, harness.GetLastResultFact()!.ResultKind);
    }

    [Fact]
    public async Task Permission_Expired_DoesNotExecuteOrMutateWorkspace()
    {
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            new ExpiredPermissionReviewService());
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.CreateFileToolName,
                """{"path":"expired.txt","content":"expired"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.False(File.Exists(Path.Combine(harness.WorkspaceRoot, "expired.txt")));
        Assert.Equal(AgentActionFailureKind.PermissionExpired, harness.GetSingleResultFact()!.FailureKind);
    }

    [Fact]
    public async Task BrokerRevoked_DoesNotExecuteOrMutateWorkspace()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        harness.Broker.Revoke();
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReadFileToolName,
                """{"path":"note.txt"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.Equal(AgentActionFailureKind.BrokerRevoked, harness.GetSingleResultFact()!.FailureKind);
    }

    [Fact]
    public async Task PolicyDenied_DoesNotExecuteOrMutateWorkspace()
    {
        var actor = ActorId.PanelSeed("self-target");
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            initiatingActorId: actor,
            targetActorId: actor);
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.CreateFileToolName,
                """{"path":"policy-denied.txt","content":"denied"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.False(File.Exists(Path.Combine(harness.WorkspaceRoot, "policy-denied.txt")));
        Assert.Equal(AgentActionFailureKind.PolicyDenied, harness.GetSingleResultFact()!.FailureKind);
    }

    [Fact]
    public async Task PreConsumeStale_RemainsPublishedDecision()
    {
        var review = new CapturingAllowingPermissionReviewService();
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            AgentFileReadResult.Rejected(AgentFileReadOutcome.NotFound, "missing"),
            AgentFileReadResult.Rejected(AgentFileReadOutcome.NotFound, "missing"),
            AgentFileReadResult.Success(
                "appeared",
                AgentContentRevision.FromUtf8Text("appeared"),
                byteLength: 8));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.Acp,
            review,
            reader);
        var absolutePath = Path.Combine(harness.WorkspaceRoot, "stale-create.txt");
        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpWriteRequest(absolutePath, "content"),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        Assert.NotNull(review.Decision);
        Assert.Equal(AgentPermissionDecisionStatus.Published, review.Decision!.Status);
        Assert.Equal(AgentActionResultKind.Revoked, harness.GetLastResultFact()!.ResultKind);
        Assert.False(File.Exists(absolutePath));
    }

    [Fact]
    public async Task PostConsumeConflict_LeavesDecisionConsumedWithoutWrite()
    {
        const string original = "base";
        var relativePath = "conflict.txt";
        var review = new CapturingAllowingPermissionReviewService();
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            AgentFileReadResult.Success(original, AgentContentRevision.FromUtf8Text(original), byteLength: 4),
            AgentFileReadResult.Success(original, AgentContentRevision.FromUtf8Text(original), byteLength: 4));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            review,
            reader,
            new StaleOnApplyMutator());
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, relativePath), original);
        var baseRevision = AgentContentRevision.FromUtf8Text(original);

        var result = await harness.Broker.RequestAsync(
            new AgentReplaceFileActionPayload(
                AgentWorkspaceRelativePath.Normalize(relativePath),
                baseRevision,
                "replacement"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Conflict, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, review.Decision!.Status);
        Assert.Equal("base", File.ReadAllText(Path.Combine(harness.WorkspaceRoot, relativePath)));
    }

    [Fact]
    public async Task SuccessfulMutation_ReportsReconciliationFact()
    {
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        var reconciler = new WorkspaceEditorDocumentReconciler(
            workspace,
            new Phase22TestEditorUiDispatcher(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceEditorDocumentReconciler>.Instance);
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            documentReconciler: reconciler);
        var relativePath = "reconcile.txt";
        var absolutePath = Path.Combine(harness.WorkspaceRoot, relativePath);
        File.WriteAllText(absolutePath, "original");
        workspace.SetProjectFromPath(harness.WorkspaceRoot);
        workspace.OpenDocument(absolutePath, "original");
        var revision = AgentContentRevision.FromUtf8Text("original");

        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReplaceFileToolName,
                $$"""{"path":"reconcile.txt","base_revision":"{{revision.Value}}","content":"replacement"}"""),
            Phase22MediatedActionTestSupport.Complete());

        Assert.Contains(
            harness.CapturedEvents,
            e => e.Kind == AgentEventKind.ActionReconciliationReported);
    }

    [Fact]
    public async Task ConcurrentAction_DeniedWithoutWorkspaceMutation()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, "note.txt"), "hello");
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
                correlationKey: null,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));
        var secondResult = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("other.txt")),
            correlationKey: null,
            CancellationToken.None);
        allowProcessingToComplete.Set();
        _ = await firstRequest;

        Assert.Equal(AgentActionFailureKind.ConcurrentActionRejected, secondResult.FailureKind);
    }

    [Fact]
    public async Task NoWorkspace_DeniedWithoutFilesystemAccess()
    {
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            hasWorkspace: false);
        var result = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionFailureKind.NoWorkspace, result.FailureKind);
        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
    }

    [Fact]
    public async Task PermissionUnavailable_DeniedWithoutMutation()
    {
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.Acp,
            new UnavailablePermissionReviewService());
        var absolutePath = Path.Combine(harness.WorkspaceRoot, "unavailable.txt");
        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpWriteRequest(absolutePath, "unavailable"),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        Assert.False(File.Exists(absolutePath));
        Assert.Equal(AgentActionFailureKind.PermissionUnavailable, harness.GetLastResultFact()!.FailureKind);
    }

    [Fact]
    public async Task NativeHarnessAndAcp_RemainIndependentSiblingBackends()
    {
        using var nativeHarness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        using var acpHarness = new Phase22MediatedActionHarness(AgentBackendIds.Acp);

        Assert.NotEqual(nativeHarness.BackendId, acpHarness.BackendId);
        Assert.NotSame(nativeHarness.Broker, acpHarness.Broker);

        var nativeTransport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            nativeTransport,
            nativeHarness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReadFileToolName,
                """{"path":"note.txt"}"""),
            Phase22MediatedActionTestSupport.Complete());

        var acpScript = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpReadRequest(
                    Path.Combine(acpHarness.WorkspaceRoot, "note.txt")),
            ],
        };
        File.WriteAllText(Path.Combine(acpHarness.WorkspaceRoot, "note.txt"), "acp");
        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(acpScript, acpHarness);

        Assert.Equal(AgentBackendIds.NativeHarness, nativeHarness.GetSingleResultAudit()!.BackendId);
        Assert.Equal(AgentBackendIds.Acp, acpHarness.GetSingleResultAudit()!.BackendId);
    }

    private sealed class StaleOnApplyMutator : IAgentFileMutator
    {
        public AgentFileMutationResult Apply(
            WorkspaceActionScope scope,
            AgentFileActionProposal proposal,
            AgentActionPayload payload,
            CancellationToken cancellationToken) =>
            AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Conflict,
                "Base content changed before the replace could be applied.");
    }

    private sealed class Phase22TestEditorUiDispatcher : Zaide.Features.Editor.Contracts.IEditorUiDispatcher
    {
        public bool WasInvoked { get; private set; }

        public void Invoke(Action action)
        {
            WasInvoked = true;
            action();
        }

        public T Invoke<T>(Func<T> func)
        {
            WasInvoked = true;
            return func();
        }

        public void Post(Action action)
        {
            WasInvoked = true;
            action();
        }
    }
}
