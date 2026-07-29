namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// User-controllable memory mutation operations admitted at M5.
/// </summary>
internal enum AgentMemoryOperationKind
{
    Create = 0,
    Correct = 1,
    Disable = 2,
    Supersede = 3,
    Delete = 4,
}
