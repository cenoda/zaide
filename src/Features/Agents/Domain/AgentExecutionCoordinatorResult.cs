using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Structured execution outcome for one admitted attempt. Payload fields are
/// validated against <see cref="ExecutionRun.Outcome"/> at construction.
/// </summary>
public sealed class AgentExecutionCoordinatorResult
{
    private AgentExecutionCoordinatorResult(
        ExecutionRun run,
        string? assistantResponse,
        string? errorMessage)
    {
        Run = run;
        AssistantResponse = assistantResponse;
        ErrorMessage = errorMessage;
    }

    public ExecutionRun Run { get; }

    public string? AssistantResponse { get; }

    public string? ErrorMessage { get; }

    public static AgentExecutionCoordinatorResult Success(ExecutionRun run, string assistantResponse)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Outcome != ExecutionRunOutcome.Success)
        {
            throw new ArgumentException(
                "Success results require a Success run outcome.",
                nameof(run));
        }

        if (string.IsNullOrWhiteSpace(assistantResponse))
        {
            throw new ArgumentException(
                "Success results require assistant response text.",
                nameof(assistantResponse));
        }

        return new AgentExecutionCoordinatorResult(run, assistantResponse, null);
    }

    public static AgentExecutionCoordinatorResult Failure(ExecutionRun run, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Outcome is not (ExecutionRunOutcome.ExecutionFailure or ExecutionRunOutcome.Cancelled))
        {
            throw new ArgumentException(
                "Failure results require an execution-failure or cancelled run outcome.",
                nameof(run));
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException(
                "Failure results require error message text.",
                nameof(errorMessage));
        }

        return new AgentExecutionCoordinatorResult(run, null, errorMessage);
    }

    public static AgentExecutionCoordinatorResult RoutingFailure(ExecutionRun run, string failureReason)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Outcome != ExecutionRunOutcome.RoutingFailure)
        {
            throw new ArgumentException(
                "Routing-failure results require a RoutingFailure run outcome.",
                nameof(run));
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "Routing-failure results require failure reason text.",
                nameof(failureReason));
        }

        return new AgentExecutionCoordinatorResult(run, null, failureReason);
    }
}
