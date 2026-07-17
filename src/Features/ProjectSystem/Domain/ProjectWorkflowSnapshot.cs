using System.Collections.Generic;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Immutable snapshot of the current project workflow slot.
/// </summary>
/// <param name="State">Whether the slot is idle, starting, or running.</param>
/// <param name="Generation">
/// Monotonically increasing operation generation. Late stdout/exit from older
/// generations must be ignored by consumers.
/// </param>
/// <param name="ActiveOperation">
/// The operation currently occupying the slot, or <c>null</c> when idle.
/// </param>
/// <param name="LastOutcome">
/// The most recent terminal outcome when idle, otherwise <c>null</c>.
/// </param>
/// <param name="TargetFilePath">
/// Absolute path of the active or last operation target, otherwise <c>null</c>.
/// </param>
/// <param name="ProcessId">
/// Child process id while running, otherwise <c>null</c>.
/// </param>
/// <param name="OutputLines">
/// Captured stdout/stderr lines for the current or most recently finished
/// operation generation. Never null.
/// </param>
/// <param name="LastOperation">
/// The operation kind most recently started or completed. Retained when idle
/// so status text can distinguish Build / Run / Test terminal outcomes.
/// <c>null</c> only before any operation has run.
/// </param>
public sealed record ProjectWorkflowSnapshot(
    ProjectWorkflowOperationState State,
    long Generation,
    ProjectWorkflowOperation? ActiveOperation,
    ProjectWorkflowOutcomeKind? LastOutcome,
    string? TargetFilePath,
    int? ProcessId,
    IReadOnlyList<ManagedProcessOutputLine> OutputLines,
    ProjectWorkflowOperation? LastOperation = null);
