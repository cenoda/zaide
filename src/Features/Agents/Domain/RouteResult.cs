namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Route outcome including resolved typed request and structured execution result
/// when an execution attempt was admitted.
/// </summary>
public sealed record RouteResult(
    bool Success,
    RouteRequest? Request,
    string? FailureReason,
    AgentExecutionCoordinatorResult? ExecutionResult);
