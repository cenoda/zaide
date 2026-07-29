namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Projected lifecycle status for one memory record revision.
/// </summary>
internal enum AgentMemoryStatus
{
    Active = 0,
    Disabled = 1,
    Superseded = 2,
    Deleted = 3,
}
