namespace Zaide.Features.Agents.Presentation.Memory;

/// <summary>
/// Explicit presentation states for the memory lifecycle surface.
/// Stale or failed reads never masquerade as <see cref="Empty"/>.
/// </summary>
internal enum AgentMemorySurfaceState
{
    Loading = 0,
    Ready = 1,
    Empty = 2,
    Unavailable = 3,
    Failed = 4,
}
