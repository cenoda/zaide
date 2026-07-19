using System;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class ExecutionRunTests
{
    [Fact]
    public void Constructor_RejectsDefaultInitiatingActorId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ExecutionRun(
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                default,
                ActorId.PanelSeed("alpha"),
                "panel-1",
                ExecutionRunOutcome.Success));

        Assert.Equal("initiatingActorId", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsDefaultTargetActorId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ExecutionRun(
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.HumanUser,
                default,
                "panel-1",
                ExecutionRunOutcome.Success));

        Assert.Equal("targetActorId", exception.ParamName);
    }
}
