namespace Zaide.Features.Agents.Domain.Continuity;

/// <summary>
/// Durable interruption classification after restart reconciliation.
/// </summary>
internal enum AgentSessionContinuityClassification
{
    Recoverable = 0,
    Terminal = 1,
    Indeterminate = 2,
}
