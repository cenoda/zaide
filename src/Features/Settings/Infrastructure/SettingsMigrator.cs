using System.Collections.Generic;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Infrastructure;

/// <summary>
/// Runs an ordered list of <see cref="ISettingsMigration"/> instances.
/// The production instance uses an empty migration list; test code supplies
/// one or more synthetic migrations to verify the infrastructure.
/// </summary>
internal sealed class SettingsMigrator
{
    private readonly IReadOnlyList<ISettingsMigration> _migrations;

    /// <param name="migrations">Ordered migrations, earliest first.</param>
    public SettingsMigrator(IReadOnlyList<ISettingsMigration> migrations)
    {
        _migrations = migrations;
    }

    /// <summary>
    /// Apply every migration in order whose <see cref="ISettingsMigration.FromVersion"/>
    /// matches the model's current <see cref="SettingsModel.SchemaVersion"/>.
    /// </summary>
    public SettingsModel Migrate(SettingsModel model)
    {
        var current = model;
        foreach (var migration in _migrations)
        {
            if (current.SchemaVersion == migration.FromVersion)
            {
                current = migration.Migrate(current);
            }
        }
        return current;
    }
}
