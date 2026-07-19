namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Terminal outcomes for the existing execution and routing paths only.
/// </summary>
public enum ExecutionRunOutcome
{
    Success,
    RoutingFailure,
    ExecutionFailure,
    Cancelled
}
