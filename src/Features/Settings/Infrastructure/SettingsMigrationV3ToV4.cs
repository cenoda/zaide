using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Infrastructure;

/// <summary>
/// Schema migration v3 → v4: adds <see cref="AgentsSettings"/> with documented
/// defaults without losing existing Editor, LLM, keybindings, or debug data.
/// </summary>
public sealed class SettingsMigrationV3ToV4 : ISettingsMigration
{
    /// <inheritdoc />
    public int FromVersion => 3;

    /// <inheritdoc />
    public int ToVersion => 4;

    /// <inheritdoc />
    public SettingsModel Migrate(SettingsModel model) =>
        model with
        {
            SchemaVersion = 4,
            Agents = AgentsSettings.Default,
        };
}
