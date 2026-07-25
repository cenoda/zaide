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

    private static ContractAgentActionBroker CreateBroker(
        AgentActionRunSlotTracker runSlot,
        AgentActionCorrelationRegistry correlationRegistry) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            AgentBackendId.FromValue("backend:test"),
            WorkspaceIdentity.New(),
            WorkspaceGeneration.Initial,
            runSlot,
            correlationRegistry);
}
