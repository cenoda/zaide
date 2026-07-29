namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Whether memory influence attribution was recorded for a run.
/// </summary>
internal enum AgentMemoryInfluenceState
{
    Recorded = 0,
    Unavailable = 1,
    NoneEligible = 2,
}
