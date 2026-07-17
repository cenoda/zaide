using System;

namespace Zaide.Features.Settings.Domain;

/// <summary>
/// Outcome of an asynchronous disk write for settings persistence.
/// Returned by <see cref="Zaide.Features.Settings.Contracts.ISettingsService.SaveAsync"/> and
/// nested inside <see cref="SettingsMutationResult"/>.
/// </summary>
public abstract record SettingsSaveResult
{
    private SettingsSaveResult() { }

    /// <summary>The write was completed successfully.</summary>
    public sealed record Saved : SettingsSaveResult;

    /// <summary>The write was skipped because a newer snapshot has already been
    /// committed; the latest state will be persisted by the newer write.</summary>
    public sealed record Superseded : SettingsSaveResult;

    /// <summary>The write failed. The <see cref="Exception"/> provides details.</summary>
    /// <param name="Exception">The exception that caused the failure.</param>
    public sealed record Failed(Exception Exception) : SettingsSaveResult;
}
