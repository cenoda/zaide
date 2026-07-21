using System;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class AgentRunStateContractTests
{
    [Theory]
    [InlineData(AgentRunStatus.Created, AgentRunStatus.Accepted, true)]
    [InlineData(AgentRunStatus.Created, AgentRunStatus.Rejected, true)]
    [InlineData(AgentRunStatus.Created, AgentRunStatus.Running, false)]
    [InlineData(AgentRunStatus.Accepted, AgentRunStatus.Running, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Completed, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Failed, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Cancelled, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.TimedOut, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Disconnected, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Indeterminate, true)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.CancellationRequested, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.Cancelled, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.Completed, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.Failed, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.TimedOut, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.Disconnected, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.Indeterminate, true)]
    [InlineData(AgentRunStatus.CancellationRequested, AgentRunStatus.Running, false)]
    [InlineData(AgentRunStatus.Completed, AgentRunStatus.Running, false)]
    [InlineData(AgentRunStatus.Rejected, AgentRunStatus.Accepted, false)]
    internal void AgentRunStatus_AllowsOnlyValidTransitions(
        AgentRunStatus from,
        AgentRunStatus to,
        bool expectedAllowed)
    {
        Assert.Equal(expectedAllowed, AgentRunStatusTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(AgentRunStatus.Rejected)]
    [InlineData(AgentRunStatus.Completed)]
    [InlineData(AgentRunStatus.Failed)]
    [InlineData(AgentRunStatus.Cancelled)]
    [InlineData(AgentRunStatus.TimedOut)]
    [InlineData(AgentRunStatus.Disconnected)]
    [InlineData(AgentRunStatus.Indeterminate)]
    internal void AgentRunStatus_TerminalStatesRejectFurtherTransitions(AgentRunStatus terminal)
    {
        foreach (AgentRunStatus candidate in Enum.GetValues<AgentRunStatus>())
        {
            if (candidate == terminal)
            {
                continue;
            }

            Assert.False(AgentRunStatusTransitions.CanTransition(terminal, candidate));
        }
    }

    [Fact]
    public void AgentRunStatus_RejectsSelfTransitionsForEveryState()
    {
        foreach (AgentRunStatus status in Enum.GetValues<AgentRunStatus>())
        {
            Assert.False(AgentRunStatusTransitions.CanTransition(status, status));
        }
    }

    [Fact]
    public void AgentRunStatusTransitions_RejectsInvalidTransition()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentRunStatusTransitions.ValidateTransition(
                AgentRunStatus.Created,
                AgentRunStatus.Completed));
    }

    [Fact]
    public void AgentRunSnapshot_BindsExactlyOneSessionAndConversation()
    {
        var runId = ExecutionRunId.New();
        var sessionId = AgentSessionId.New();
        var conversationId = ConversationId.NewDirect();
        var correlationId = ConversationEntryCorrelationId.FromValue(runId.Value);

        var snapshot = new AgentRunSnapshot(
            runId,
            sessionId,
            conversationId,
            correlationId,
            AgentRunStatus.Accepted);

        Assert.Equal(runId, snapshot.RunId);
        Assert.Equal(sessionId, snapshot.SessionId);
        Assert.Equal(conversationId, snapshot.ConversationId);
        Assert.Equal(correlationId, snapshot.CorrelationId);
    }

    [Fact]
    public void AgentRunSnapshot_RejectsMismatchedCorrelationIdentity()
    {
        var runId = ExecutionRunId.New();
        var otherCorrelation = ConversationEntryCorrelationId.FromValue("run:other");

        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentRunSnapshot(
                runId,
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                otherCorrelation,
                AgentRunStatus.Created));

        Assert.Equal("correlationId", exception.ParamName);
    }

    [Fact]
    public void AgentRunSnapshot_RejectsUndefinedStatus()
    {
        var runId = ExecutionRunId.New();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AgentRunSnapshot(
                runId,
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                ConversationEntryCorrelationId.FromValue(runId.Value),
                (AgentRunStatus)999));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void AgentRunSnapshot_RejectsDefaultRunId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentRunSnapshot(
                default,
                AgentSessionId.New(),
                ConversationId.NewDirect(),
                ConversationEntryCorrelationId.FromValue("run:placeholder"),
                AgentRunStatus.Created));

        Assert.Equal("runId", exception.ParamName);
    }
}
