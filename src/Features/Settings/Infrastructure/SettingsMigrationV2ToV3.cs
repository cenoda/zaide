
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
namespace Zaide.Features.Settings.Infrastructure;

/// <summary>
/// Schema migration v2 → v3: adds an empty
/// <see cref="DebugSettings.BreakpointsByWorkspaceRoot"/> map without losing
/// existing settings.
/// </summary>
public sealed class SettingsMigrationV2ToV3 : ISettingsMigration
{
    /// <inheritdoc />
    public int FromVersion => 2;

    /// <inheritdoc />
    public int ToVersion => 3;

    /// <inheritdoc />
    public SettingsModel Migrate(SettingsModel model) =>
        model with
        {
            SchemaVersion = 3,
            Debug = DebugSettings.Default,
        };
}