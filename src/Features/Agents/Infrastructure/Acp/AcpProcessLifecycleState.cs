namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Lifecycle state for an owned ACP stdio process host.
/// </summary>
internal enum AcpProcessLifecycleState
{
    Starting,
    Running,
    ProcessExited,
    Disposed,
}
