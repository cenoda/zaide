namespace Zaide.Features.Agents.Domain.Continuity;

internal enum AgentSessionContinuityOperationStatus
{
    Accepted = 0,
    DuplicateIgnored = 1,
    Rejected = 2,
    Indeterminate = 3,
}
