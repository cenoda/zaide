using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Settings.Domain;

namespace Zaide.Features.Settings.Contracts;

/// <summary>
/// Thread-safe singleton service managing the immutable, versioned settings
/// model with atomic persistence and last-known-good recovery.
/// </summary>
public interface ISettingsService
{
    /// <summary>Frozen, never-null snapshot of the current settings.
    /// Updated atomically before <see cref="WhenChanged"/> emits.</summary>
    SettingsModel Current { get; }

    /// <summary>Emits a new immutable snapshot on every committed change.
    /// Emitted on the mutating thread (not UI-marshalled). UI subscribers
    /// apply <c>ObserveOn(RxApp.MainThreadScheduler)</c>.</summary>
    IObservable<SettingsModel> WhenChanged { get; }

    /// <summary>Outcome of the initial settings-file load.</summary>
    SettingsLoadResult LoadResult { get; }

    /// <summary>
    /// Apply a validated pure transformation. The producer receives the current
    /// immutable snapshot and returns a new instance (via <c>with</c>).
    /// Producer execution, validation, and the in-memory commit occur inside
    /// a mutation gate, so concurrent producers compose correctly.
    /// The returned task completes when the queued write is consumed; it never
    /// faults from a write failure — inspect <see cref="SettingsMutationResult"/>
    /// for the disk outcome.
    /// Throws <see cref="OperationCanceledException"/> only before the mutation
    /// gate is acquired.
    /// </summary>
    Task<SettingsMutationResult> UpdateAsync(
        Func<SettingsModel, SettingsModel> producer,
        CancellationToken ct = default);

    /// <summary>
    /// Apply an already-constructed snapshot. Inside the mutation gate, commits
    /// only when <paramref name="expectedCurrent"/> is reference-identical to
    /// <see cref="Current"/>; otherwise returns <see cref="SettingsMutationResult.Conflict"/>
    /// without overwriting a concurrent change. Invalid candidates return
    /// <see cref="SettingsMutationResult.Invalid"/> and leave Current unchanged.
    /// Throws <see cref="OperationCanceledException"/> only before the gate.
    /// </summary>
    Task<SettingsMutationResult> ApplyAsync(
        SettingsModel expectedCurrent,
        SettingsModel next,
        CancellationToken ct = default);

    /// <summary>Persist the current in-memory snapshot without mutation.
    /// Throws <see cref="OperationCanceledException"/> only before enqueue.</summary>
    Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default);

    /// <summary>Fires on the writer-loop thread when a disk write fails.
    /// Does NOT fire for successful writes.</summary>
    IObservable<SettingsSaveError> WriteErrors { get; }
}
