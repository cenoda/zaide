namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Terminal completion classification for one Native Harness run attempt.
/// Maps to session run status and <see cref="AgentBackendEvent"/> emission in M3.
/// </summary>
internal enum NativeHarnessRunTerminationKind
{
    Completed,
    Failed,
    Cancelled,
    Indeterminate,
    TurnBudgetExceeded,
}
