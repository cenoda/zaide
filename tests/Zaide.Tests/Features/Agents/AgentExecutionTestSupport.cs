using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Shared helpers for structured execution coordinator/router tests.
/// </summary>
internal static class AgentExecutionTestSupport
{
    public static AgentExecutionCoordinatorResult SuccessResult(
        AgentPanelState panel,
        string assistantResponse = "Hello back")
    {
        return new AgentExecutionCoordinatorResult(
            new ExecutionRun(
                ExecutionRunId.New(),
                panel.ConversationId,
                ActorId.HumanUser,
                panel.ActorId,
                panel.PanelId,
                ExecutionRunOutcome.Success),
            assistantResponse,
            null);
    }

    public static AgentExecutionCoordinatorResult ErrorResult(
        AgentPanelState panel,
        string errorMessage = "Request failed",
        ExecutionRunOutcome outcome = ExecutionRunOutcome.ExecutionFailure)
    {
        return new AgentExecutionCoordinatorResult(
            new ExecutionRun(
                ExecutionRunId.New(),
                panel.ConversationId,
                ActorId.HumanUser,
                panel.ActorId,
                panel.PanelId,
                outcome),
            null,
            errorMessage);
    }
}
