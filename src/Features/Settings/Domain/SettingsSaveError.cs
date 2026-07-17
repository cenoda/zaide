using System;

namespace Zaide.Features.Settings.Domain;

/// <summary>
/// Published via <see cref="Zaide.Features.Settings.Contracts.ISettingsService.WriteErrors"/> when a disk
/// write fails. Emitted on the writer-loop thread (not UI-marshalled).
/// </summary>
/// <param name="Exception">The exception that caused the write failure.</param>
/// <param name="FailedSnapshot">
/// The <see cref="SettingsModel"/> snapshot that failed to persist.
/// </param>
/// <param name="Timestamp">UTC time of the failure.</param>
public sealed record SettingsSaveError(
    Exception Exception,
    SettingsModel FailedSnapshot,
    DateTime Timestamp);
