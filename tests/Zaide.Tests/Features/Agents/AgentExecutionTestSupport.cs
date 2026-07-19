using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Shared helpers for structured execution coordinator/router tests.
/// </summary>
internal static class AgentExecutionTestSupport
{
    public static AgentExecutionCoordinator CreateCoordinator(
        AgentPanelHost host,
        IAgentExecutionService executionService,
        IConversationStore? conversationStore = null) =>
        new(
            host,
            executionService,
            conversationStore ?? Conversations.ConversationsTestSupport.CreateStore());
    public static AgentExecutionCoordinatorResult SuccessResult(
        AgentPanelState panel,
        string assistantResponse = "Hello back")
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            ExecutionRunOutcome.Success);

        return AgentExecutionCoordinatorResult.Success(run, assistantResponse);
    }

    public static AgentExecutionCoordinatorResult ErrorResult(
        AgentPanelState panel,
        string errorMessage = "Request failed",
        ExecutionRunOutcome outcome = ExecutionRunOutcome.ExecutionFailure)
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            outcome);

        return AgentExecutionCoordinatorResult.Failure(run, errorMessage);
    }

    public static AgentExecutionCoordinatorResult RoutingFailureResult(
        AgentPanelState panel,
        string failureReason = "Unknown target")
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            ExecutionRunOutcome.RoutingFailure);

        return AgentExecutionCoordinatorResult.RoutingFailure(run, failureReason);
    }
}
