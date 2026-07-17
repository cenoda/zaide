namespace Zaide.Features.Settings.Domain;

/// <summary>
/// Outcome of the initial settings file load performed during
/// <see cref="Zaide.Features.Settings.Contracts.ISettingsService"/> construction.
/// </summary>
public enum SettingsLoadResult
{
    /// <summary>The settings file was not found; defaults are used.</summary>
    Missing,

    /// <summary>The settings file was found but could not be parsed; the
    /// last-known-good file (or defaults) was used instead.</summary>
    Corrupt,

    /// <summary>The schema version is too old or too new to be loaded safely;
    /// defaults are used.</summary>
    UnsupportedVersion,

    /// <summary>Settings loaded successfully from the file.</summary>
    Loaded
}
