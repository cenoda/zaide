using System;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class AgentSessionContractTests
{
    [Fact]
    public void AgentSessionId_New_CreatesPrefixedNonDefaultValue()
    {
        var id = AgentSessionId.New();

        Assert.NotEqual(default(AgentSessionId), id);
        Assert.StartsWith("session:", id.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AgentSessionId_FromValue_RejectsMissingValue(string? value)
    {
        Assert.Throws<ArgumentException>(() => AgentSessionId.FromValue(value!));
    }

    [Fact]
    public void AgentSessionSnapshot_EnforcesOneConversationOneAgentOneBackend()
    {
        var sessionId = AgentSessionId.New();
        var conversationId = ConversationId.NewDirect();
        var agentId = ActorId.PanelSeed("alpha");
        var backendId = AgentBackendId.FromValue("backend:legacy-openai-compatible");
        var capability = CreateCapabilitySnapshot(backendId);

        var snapshot = new AgentSessionSnapshot(
            sessionId,
            conversationId,
            agentId,
            backendId,
            "1.0.0",
            AgentSessionStatus.Ready,
            capability,
            activeRunId: null);

        Assert.Equal(conversationId, snapshot.ConversationId);
        Assert.Equal(agentId, snapshot.AgentIdentity);
        Assert.Equal(backendId, snapshot.BackendId);
        Assert.Equal("1.0.0", snapshot.BackendVersion);
    }

    [Fact]
    public void AgentSessionSnapshot_RejectsMismatchedCapabilityBackend()
    {
        var sessionId = AgentSessionId.New();
        var conversationId = ConversationId.NewDirect();
        var sessionBackend = AgentBackendId.FromValue("backend:legacy-openai-compatible");
        var otherBackend = AgentBackendId.FromValue("backend:other");
        var capability = CreateCapabilitySnapshot(otherBackend);

        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentSessionSnapshot(
                sessionId,
                conversationId,
                ActorId.PanelSeed("alpha"),
                sessionBackend,
                "1.0.0",
                AgentSessionStatus.Ready,
                capability,
                activeRunId: null));

        Assert.Equal("capabilitySnapshot", exception.ParamName);
    }

    [Theory]
    [InlineData(AgentSessionStatus.Ready, AgentSessionStatus.Running, true)]
    [InlineData(AgentSessionStatus.Running, AgentSessionStatus.Ready, true)]
    [InlineData(AgentSessionStatus.Running, AgentSessionStatus.Ending, true)]
    [InlineData(AgentSessionStatus.Ending, AgentSessionStatus.Ended, true)]
    [InlineData(AgentSessionStatus.Ready, AgentSessionStatus.Ending, true)]
    [InlineData(AgentSessionStatus.Ready, AgentSessionStatus.Ended, true)]
    [InlineData(AgentSessionStatus.Ended, AgentSessionStatus.Ready, false)]
    [InlineData(AgentSessionStatus.Running, AgentSessionStatus.Ended, false)]
    internal void AgentSessionStatus_AllowsOnlyValidTransitions(
        AgentSessionStatus from,
        AgentSessionStatus to,
        bool expectedAllowed)
    {
        Assert.Equal(expectedAllowed, AgentSessionStatusTransitions.CanTransition(from, to));
    }

    [Fact]
    public void AgentSessionStatusTransitions_RejectsInvalidTransition()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentSessionStatusTransitions.ValidateTransition(
                AgentSessionStatus.Ended,
                AgentSessionStatus.Running));
    }

    [Fact]
    public void AgentSessionSnapshot_RejectsUndefinedStatus()
    {
        var backendId = AgentBackendId.FromValue("backend:legacy-openai-compatible");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AgentSessionSnapshot(
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                ActorId.PanelSeed("alpha"),
                backendId,
                "1.0.0",
                (AgentSessionStatus)999,
                CreateCapabilitySnapshot(backendId),
                activeRunId: null));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void AgentSessionSnapshot_RejectsDefaultActiveRunId()
    {
        var backendId = AgentBackendId.FromValue("backend:legacy-openai-compatible");

        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentSessionSnapshot(
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                ActorId.PanelSeed("alpha"),
                backendId,
                "1.0.0",
                AgentSessionStatus.Ending,
                CreateCapabilitySnapshot(backendId),
                activeRunId: default(ExecutionRunId)));

        Assert.Equal("activeRunId", exception.ParamName);
    }

    [Fact]
    public void AgentSessionSnapshot_RejectsRunningWithoutActiveRun()
    {
        var backendId = AgentBackendId.FromValue("backend:legacy-openai-compatible");

        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentSessionSnapshot(
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                ActorId.PanelSeed("alpha"),
                backendId,
                "1.0.0",
                AgentSessionStatus.Running,
                CreateCapabilitySnapshot(backendId),
                activeRunId: null));

        Assert.Equal("activeRunId", exception.ParamName);
    }

    [Fact]
    public void AgentSessionSnapshot_RejectsReadyWithActiveRun()
    {
        var backendId = AgentBackendId.FromValue("backend:legacy-openai-compatible");

        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentSessionSnapshot(
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                ActorId.PanelSeed("alpha"),
                backendId,
                "1.0.0",
                AgentSessionStatus.Ready,
                CreateCapabilitySnapshot(backendId),
                activeRunId: ExecutionRunId.New()));

        Assert.Equal("activeRunId", exception.ParamName);
    }

    [Fact]
    public void AgentSessionStatus_RejectsSelfTransitionsForEveryState()
    {
        foreach (AgentSessionStatus status in Enum.GetValues<AgentSessionStatus>())
        {
            Assert.False(AgentSessionStatusTransitions.CanTransition(status, status));
        }
    }

    private static AgentCapabilitySnapshot CreateCapabilitySnapshot(AgentBackendId backendId) =>
        AgentCapabilitySnapshot.CreateInitial(
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
