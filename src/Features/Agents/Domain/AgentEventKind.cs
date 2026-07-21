namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Normalized Agent Session event taxonomy for Phase 15 lifecycle, message,
/// failure, and capability observations.
/// </summary>
internal enum AgentEventKind
{
    SessionReady,
    SessionRunning,
    SessionEnding,
    SessionEnded,

    RunCreated,
    RunAccepted,
    RunRejected,
    RunRunning,
    RunCancellationRequested,
    RunCompleted,
    RunFailed,
    RunCancelled,
    RunTimedOut,
    RunDisconnected,
    RunIndeterminate,

    UserMessageAdmitted,
    AssistantMessageCompleted,

    FailureReported,
    CapabilitySnapshotChanged,
}
