using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Terminal outcome for one Native Harness run attempt before backend-event emission.
/// </summary>
internal sealed class NativeHarnessRunOutcome
{
    public NativeHarnessRunOutcome(
        NativeHarnessRunTerminationKind terminationKind,
        string? finalAssistantText = null,
        string? failureReason = null,
        NativeHarnessLateCompletionDisposition lateCompletionDisposition =
            NativeHarnessLateCompletionDisposition.None)
    {
        if (!Enum.IsDefined(terminationKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminationKind),
                terminationKind,
                "Termination kind is invalid.");
        }

        if (!Enum.IsDefined(lateCompletionDisposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lateCompletionDisposition),
                lateCompletionDisposition,
                "Late completion disposition is invalid.");
        }

        if (terminationKind == NativeHarnessRunTerminationKind.Completed
            && string.IsNullOrWhiteSpace(finalAssistantText))
        {
            throw new ArgumentException(
                "Completed runs require final assistant text.",
                nameof(finalAssistantText));
        }

        if (terminationKind is NativeHarnessRunTerminationKind.Failed
            or NativeHarnessRunTerminationKind.TurnBudgetExceeded
            && string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "Failed runs require a failure reason.",
                nameof(failureReason));
        }

        if (terminationKind == NativeHarnessRunTerminationKind.Indeterminate
            && lateCompletionDisposition == NativeHarnessLateCompletionDisposition.None
            && string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "Indeterminate runs require a failure reason or late-completion disposition.",
                nameof(failureReason));
        }

        TerminationKind = terminationKind;
        FinalAssistantText = finalAssistantText;
        FailureReason = failureReason;
        LateCompletionDisposition = lateCompletionDisposition;
    }

    public NativeHarnessRunTerminationKind TerminationKind { get; }

    public string? FinalAssistantText { get; }

    public string? FailureReason { get; }

    public NativeHarnessLateCompletionDisposition LateCompletionDisposition { get; }

    public bool IsTerminal => true;
}
