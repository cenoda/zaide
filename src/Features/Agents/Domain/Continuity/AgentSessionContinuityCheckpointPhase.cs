namespace Zaide.Features.Agents.Domain.Continuity;

/// <summary>
/// Material lifecycle transition points that require durable checkpoints.
/// </summary>
internal enum AgentSessionContinuityCheckpointPhase
{
    BeforeSessionStart = 0,
    AfterSessionReady = 1,
    BeforeRunStart = 2,
    AfterRunTerminal = 3,
    BeforeApplicationShutdown = 4,
    AfterStartupReconcile = 5,
}
