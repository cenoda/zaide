using System.Collections.Generic;

namespace Zaide.Features.Settings.Domain;

/// <summary>
/// Result of a settings mutation operation
/// (<see cref="Zaide.Features.Settings.Contracts.ISettingsService.UpdateAsync"/> or
/// <see cref="Zaide.Features.Settings.Contracts.ISettingsService.ApplyAsync"/>).
/// </summary>
public abstract record SettingsMutationResult
{
    private SettingsMutationResult() { }

    /// <summary>The mutation was committed in-memory and queued for persistence.</summary>
    /// <param name="Current">The newly committed snapshot.</param>
    /// <param name="SaveResult">Outcome of the queued disk write.</param>
    public sealed record Applied(
        SettingsModel Current,
        SettingsSaveResult SaveResult
    ) : SettingsMutationResult;

    /// <summary>The candidate failed validation and was not committed.</summary>
    /// <param name="Rejected">The rejected candidate snapshot.</param>
    /// <param name="Errors">Field-level validation errors.</param>
    public sealed record Invalid(
        SettingsModel Rejected,
        IReadOnlyList<SettingsValidationError> Errors
    ) : SettingsMutationResult;

    /// <summary>The candidate's expected base snapshot did not match <c>Current</c>;
    /// the caller should refresh and retry.</summary>
    /// <param name="Current">The current committed snapshot.</param>
    public sealed record Conflict(
        SettingsModel Current
    ) : SettingsMutationResult;
}
