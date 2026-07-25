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

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M2 — run/action authority binding to workspace generation and
/// bounded read execution through the broker: allowed reads execute, stale
/// generations revoke before execution, duplicate requests do not re-execute,
/// and reader outcomes map to terminal results.
/// </summary>
public sealed class Phase17WorkspaceReadBrokerTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceActionScope _scope;

    public Phase17WorkspaceReadBrokerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zaide-p17-broker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _scope = new WorkspaceActionScope(
            WorkspaceIdentity.New(),
            WorkspaceGeneration.Initial,
            _root);
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

    [Fact]
    public async Task AllowedRead_ExecutesThroughReader_ReturnsSucceeded()
    {
        File.WriteAllText(Path.Combine(_root, "note.txt"), "hello");
        var broker = CreateBroker(new WorkspaceFileReader(), new FakeWorkspaceActionAuthority(_scope));

        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.Null(result.FailureKind);
        Assert.Contains("revision", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleWorkspaceGeneration_RevokesReadBeforeExecution()
    {
        var reader = new CountingAgentFileReader();
        var authority = new FakeWorkspaceActionAuthority(_scope) { IsStale = true };
        var broker = CreateBroker(reader, authority);

        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleWorkspace, result.FailureKind);
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task DuplicateCorrelationKey_DoesNotReExecuteRead()
    {
        var reader = new CountingAgentFileReader();
        var broker = CreateBroker(reader, new FakeWorkspaceActionAuthority(_scope));
        const string correlationKey = "read-dup-1";
        var payload = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt"));

        var first = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);
        var second = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
        Assert.Equal(AgentActionResultKind.DuplicateReplay, second.ResultKind);
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task ReaderCancelledOutcome_MapsToCancelledResult()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Rejected(AgentFileReadOutcome.Cancelled, "cancelled during read"));
        var broker = CreateBroker(reader, new FakeWorkspaceActionAuthority(_scope));

        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Cancelled, result.ResultKind);
    }

    [Fact]
    public async Task ReaderPathEscape_MapsToFailedPathRejected()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Rejected(AgentFileReadOutcome.PathEscaped, "escaped"));
        var broker = CreateBroker(reader, new FakeWorkspaceActionAuthority(_scope));

        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Failed, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PathRejected, result.FailureKind);
    }

    [Fact]
    public async Task ReaderTooLarge_MapsToFailedBudgetExceeded()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Rejected(AgentFileReadOutcome.TooLarge, "too large"));
        var broker = CreateBroker(reader, new FakeWorkspaceActionAuthority(_scope));

        var result = await broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Failed, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.BudgetExceeded, result.FailureKind);
    }

    private ContractAgentActionBroker CreateBroker(
        IAgentFileReader reader,
        IWorkspaceActionAuthority authority) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            AgentBackendId.FromValue("backend:test"),
            _scope,
            authority,
            reader,
            new FakeTrustedCommandResolver(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry());
}
