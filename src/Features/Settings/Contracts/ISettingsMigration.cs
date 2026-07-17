
using Zaide.Features.Settings.Domain;
namespace Zaide.Features.Settings.Contracts;

/// <summary>
/// A pure, ordered migration that transforms a <see cref="SettingsModel"/>
/// from one schema version to the next.
/// Migrations are idempotent in aggregate: running all migrations in order
/// from the persisted schema version up to the current version produces the
/// current schema.
/// </summary>
public interface ISettingsMigration
{
    /// <summary>The source schema version this migration applies to.</summary>
    int FromVersion { get; }

    /// <summary>The target schema version after migration.</summary>
    int ToVersion { get; }

    /// <summary>Transform the model. Must not mutate <paramref name="model"/>.</summary>
    SettingsModel Migrate(SettingsModel model);
}
