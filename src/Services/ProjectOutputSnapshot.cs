using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Immutable projection of structured workflow process output for the Output surface.
/// </summary>
/// <param name="Generation">
/// Operation generation. Lines from other generations must be ignored by consumers.
/// </param>
/// <param name="State">Whether the workflow slot is idle, starting, or running.</param>
/// <param name="ActiveOperation">
/// The operation occupying the slot, or <c>null</c> when idle.
/// </param>
/// <param name="LastOutcome">
/// The most recent terminal outcome when idle, otherwise <c>null</c>.
/// </param>
/// <param name="TargetFilePath">
/// Absolute path of the active or last operation target, otherwise <c>null</c>.
/// </param>
/// <param name="Lines">Captured stdout/stderr lines for the current generation.</param>
/// <param name="LastOperation">
/// The operation kind most recently started or completed (for status text when idle).
/// </param>
public sealed record ProjectOutputSnapshot(
    long Generation,
    ProjectWorkflowOperationState State,
    ProjectWorkflowOperation? ActiveOperation,
    ProjectWorkflowOutcomeKind? LastOutcome,
    string? TargetFilePath,
    IReadOnlyList<ManagedProcessOutputLine> Lines,
    ProjectWorkflowOperation? LastOperation = null);
