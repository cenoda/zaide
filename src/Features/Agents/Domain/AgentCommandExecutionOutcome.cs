namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Terminal outcome for one bounded Phase 17 command execution attempt.
/// </summary>
internal enum AgentCommandExecutionOutcome
{
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    StartupFailed,
    Truncated,
    PathEscaped,
    DeniedExecutable,
    Unreadable,
    IndeterminateCleanup,
}
