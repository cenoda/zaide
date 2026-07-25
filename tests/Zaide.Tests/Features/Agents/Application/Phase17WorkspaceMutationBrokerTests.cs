using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M5 — broker integration for accepted proposal mutation execution.
/// </summary>
public sealed class Phase17WorkspaceMutationBrokerTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly WorkspaceFileReader _fileReader;
    private readonly WorkspaceFileMutator _fileMutator;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;

    public Phase17WorkspaceMutationBrokerTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p17-mut-broker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
        _workspaceAuthority = new FakeWorkspaceActionAuthority(_scope);
        _fileReader = new WorkspaceFileReader();
        _fileMutator = new WorkspaceFileMutator();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task ApprovedCreate_MutatesDiskOnce()
    {
        const string content = "broker create";
        var broker = CreateBroker();
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("broker-create.txt"),
            content);

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.Equal(content, File.ReadAllText(Path.Combine(_workspaceRoot, "broker-create.txt")));
        Assert.Equal(payload.ProposedRevision, result.Revision);
    }

    [Fact]
    public async Task ApprovedReplace_MutatesDiskWhenBaseMatches()
    {
        const string original = "original";
        const string replacement = "replacement";
        var path = "broker-replace.txt";
        File.WriteAllText(Path.Combine(_workspaceRoot, path), original);
        var baseRevision = AgentContentRevision.FromUtf8Text(original);
        var broker = CreateBroker();
        var payload = new AgentReplaceFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(path),
            baseRevision,
            replacement);

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.Equal(replacement, File.ReadAllText(Path.Combine(_workspaceRoot, path)));
    }

    [Fact]
    public async Task ApprovedDelete_RemovesDiskFileWhenBaseMatches()
    {
        var path = "broker-delete.txt";
        const string content = "delete me";
        File.WriteAllText(Path.Combine(_workspaceRoot, path), content);
        var broker = CreateBroker();
        var payload = new AgentDeleteFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(path),
            AgentContentRevision.FromUtf8Text(content));

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.False(File.Exists(Path.Combine(_workspaceRoot, path)));
    }

    [Fact]
    public async Task ApplyTimeStaleBase_ReturnsConflictAfterConsumption()
    {
        const string original = "original";
        var path = "apply-stale.txt";
        File.WriteAllText(Path.Combine(_workspaceRoot, path), original);
        var baseRevision = AgentContentRevision.FromUtf8Text(original);
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            AgentFileReadResult.Success(original, baseRevision, byteLength: original.Length),
            AgentFileReadResult.Success(original, baseRevision, byteLength: original.Length));
        var mutator = new StaleOnApplyMutator();
        var broker = CreateBroker(reader, mutator);
        var payload = new AgentReplaceFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(path),
            baseRevision,
            "replacement");

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Conflict, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
        Assert.Equal(original, File.ReadAllText(Path.Combine(_workspaceRoot, path)));
        Assert.Equal(1, mutator.ApplyCount);
    }

    [Fact]
    public async Task StaleWorkspaceBeforeApply_ReturnsRevokedWithoutMutation()
    {
        var mutator = new CountingAgentFileMutator();
        var authority = new FakeWorkspaceActionAuthority(_scope) { IsStale = true };
        var broker = CreateBroker(_fileReader, mutator, authority);
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("stale-ws.txt"),
            "content");

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleWorkspace, result.FailureKind);
        Assert.Equal(0, mutator.ApplyCount);
    }

    [Fact]
    public async Task DuplicateCorrelationKey_DoesNotReExecuteMutation()
    {
        var mutator = new CountingAgentFileMutator();
        var broker = CreateBroker(_fileReader, mutator);
        const string correlationKey = "mutation-dup";
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("dup-create.txt"),
            "once");

        var first = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);
        var second = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
        Assert.Equal(AgentActionResultKind.DuplicateReplay, second.ResultKind);
        Assert.Equal(1, mutator.ApplyCount);
    }

    [Fact]
    public async Task MutationFailure_ReturnsFailedWithoutSucceededClaim()
    {
        var mutator = new CountingAgentFileMutator(
            AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                "rename failed"));
        var broker = CreateBroker(_fileReader, mutator);
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("failed-create.txt"),
            "content");

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Failed, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.ExecutionFailed, result.FailureKind);
        Assert.False(File.Exists(Path.Combine(_workspaceRoot, "failed-create.txt")));
    }

    private ContractAgentActionBroker CreateBroker(
        IAgentFileReader? reader = null,
        IAgentFileMutator? mutator = null,
        IWorkspaceActionAuthority? authority = null) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            authority ?? _workspaceAuthority,
            reader ?? _fileReader,
            mutator ?? _fileMutator,
            new DefaultAgentCommandResolver(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            new AllowingPermissionReviewService());

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
    }

    private sealed class StaleOnApplyMutator : IAgentFileMutator
    {
        public int ApplyCount { get; private set; }

        public AgentFileMutationResult Apply(
            WorkspaceActionScope scope,
            AgentFileActionProposal proposal,
            AgentActionPayload payload,
            CancellationToken cancellationToken)
        {
            ApplyCount++;
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Conflict,
                "Base content changed before the replace could be applied.");
        }
    }
}
