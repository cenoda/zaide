namespace Zaide.Features.Agents.Domain.Continuity;

/// <summary>
/// Explicit user or system continuity operations. These are distinct
/// product intents and must not be collapsed.
/// </summary>
internal enum AgentSessionContinuityOperationKind
{
    Reconcile = 0,
    Resume = 1,
    Terminate = 2,
    Abandon = 3,
    Archive = 4,
    Reconnect = 5,
    Retry = 6,
    Replay = 7,
    NewSession = 8,
    Checkpoint = 9,
}
