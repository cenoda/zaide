using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Run-scoped cancellation semantics for the Native Harness loop.
/// </summary>
internal sealed class NativeHarnessCancellationState
{
    private NativeHarnessCancellationState(
        bool cancellationRequested,
        NativeHarnessLateCompletionDisposition lateCompletionDisposition)
    {
        if (!Enum.IsDefined(lateCompletionDisposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lateCompletionDisposition),
                lateCompletionDisposition,
                "Late completion disposition is invalid.");
        }

        CancellationRequested = cancellationRequested;
        LateCompletionDisposition = lateCompletionDisposition;
    }

    public bool CancellationRequested { get; }

    public NativeHarnessLateCompletionDisposition LateCompletionDisposition { get; }

    public bool HasLateCompletion =>
        LateCompletionDisposition != NativeHarnessLateCompletionDisposition.None;

    public static NativeHarnessCancellationState Initial() =>
        new(cancellationRequested: false, NativeHarnessLateCompletionDisposition.None);

    public NativeHarnessCancellationState WithCancellationRequested() =>
        new(cancellationRequested: true, LateCompletionDisposition);

    public NativeHarnessCancellationState WithLateCompletionObserved(
        NativeHarnessLateCompletionDisposition disposition)
    {
        if (disposition == NativeHarnessLateCompletionDisposition.None)
        {
            throw new ArgumentException(
                "Late completion disposition must be observed.",
                nameof(disposition));
        }

        return new NativeHarnessCancellationState(
            cancellationRequested: true,
            disposition);
    }
}
