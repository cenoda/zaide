namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Structured coordinator outcome for one admitted execution attempt.
/// Carries typed run identity, target identity, terminal outcome, and the
/// assistant or error payload required by current rendering.
/// </summary>
public sealed record AgentExecutionCoordinatorResult(
    ExecutionRun Run,
    string? AssistantResponse,
    string? ErrorMessage);
