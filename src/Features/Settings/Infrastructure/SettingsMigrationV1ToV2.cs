
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
namespace Zaide.Features.Settings.Infrastructure;

/// <summary>
/// Schema migration v1 → v2: adds <see cref="EditorSettings.FormatOnSave"/> default
/// <c>false</c> without losing existing settings.
/// </summary>
public sealed class SettingsMigrationV1ToV2 : ISettingsMigration
{
    /// <inheritdoc />
    public int FromVersion => 1;

    /// <inheritdoc />
    public int ToVersion => 2;

    /// <inheritdoc />
    public SettingsModel Migrate(SettingsModel model)
    {
        // `with` keeps all existing fields; FormatOnSave defaults to false when
        // the source model lacked the property (deserialized as default false).
        return model with
        {
            SchemaVersion = 2,
            Editor = model.Editor with { FormatOnSave = false },
        };
    }
}
