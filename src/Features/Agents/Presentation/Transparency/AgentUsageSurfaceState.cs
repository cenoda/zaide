namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Explicit presentation states for the usage and cost surface.
/// Stale or failed reads never masquerade as <see cref="Empty"/>.
/// </summary>
internal enum AgentUsageSurfaceState
{
    Loading = 0,
    Ready = 1,
    Empty = 2,
    Unavailable = 3,
    Failed = 4,
}
