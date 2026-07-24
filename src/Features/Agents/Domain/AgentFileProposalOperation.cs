namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable non-mutating file proposal operation.
/// </summary>
internal enum AgentFileProposalOperation
{
    Create,
    Replace,
    Delete,
}
