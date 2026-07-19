using System;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class AgentExecutionCoordinatorResultTests
{
    private static ExecutionRun CreateRun(ExecutionRunOutcome outcome) =>
        new(
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            "panel-1",
            outcome);

    [Fact]
    public void Success_RejectsMissingAssistantResponse()
    {
        var run = CreateRun(ExecutionRunOutcome.Success);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.Success(run, "   "));
    }

    [Fact]
    public void Success_RejectsNonSuccessOutcome()
    {
        var run = CreateRun(ExecutionRunOutcome.ExecutionFailure);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.Success(run, "Hello"));
    }

    [Fact]
    public void Failure_RejectsSuccessOutcome()
    {
        var run = CreateRun(ExecutionRunOutcome.Success);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.Failure(run, "boom"));
    }

    [Fact]
    public void Failure_RejectsRoutingFailureOutcome()
    {
        var run = CreateRun(ExecutionRunOutcome.RoutingFailure);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.Failure(run, "boom"));
    }

    [Fact]
    public void Failure_RejectsMissingErrorMessage()
    {
        var run = CreateRun(ExecutionRunOutcome.ExecutionFailure);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.Failure(run, ""));
    }

    [Fact]
    public void RoutingFailure_RejectsSuccessOutcome()
    {
        var run = CreateRun(ExecutionRunOutcome.Success);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.RoutingFailure(run, "Unknown target"));
    }

    [Fact]
    public void RoutingFailure_RejectsMissingFailureReason()
    {
        var run = CreateRun(ExecutionRunOutcome.RoutingFailure);

        Assert.Throws<ArgumentException>(() =>
            AgentExecutionCoordinatorResult.RoutingFailure(run, " "));
    }

    [Fact]
    public void Success_ExposesOnlyAssistantPayload()
    {
        var result = AgentExecutionCoordinatorResult.Success(CreateRun(ExecutionRunOutcome.Success), "Hello");

        Assert.Equal("Hello", result.AssistantResponse);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(ExecutionRunOutcome.Success, result.Run.Outcome);
    }

    [Fact]
    public void RoutingFailure_ExposesOnlyFailurePayload()
    {
        var result = AgentExecutionCoordinatorResult.RoutingFailure(
            CreateRun(ExecutionRunOutcome.RoutingFailure),
            "Unknown target");

        Assert.Null(result.AssistantResponse);
        Assert.Equal("Unknown target", result.ErrorMessage);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.Run.Outcome);
    }
}
