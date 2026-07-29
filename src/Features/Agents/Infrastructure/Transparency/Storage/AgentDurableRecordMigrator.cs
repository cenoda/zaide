using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Contracts.Transparency;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// Runs ordered partition-index migrations with backup-before-migration semantics
/// enforced by the file store caller.
/// </summary>
internal sealed class AgentDurableRecordMigrator
{
    private readonly IReadOnlyList<IAgentDurableRecordMigration> _migrations;

    public AgentDurableRecordMigrator(IReadOnlyList<IAgentDurableRecordMigration> migrations)
    {
        _migrations = migrations;
    }

    public (string IndexJson, bool Migrated) Migrate(string indexJson, int currentVersion)
    {
        var migrated = false;
        var working = indexJson;
        var version = currentVersion;

        foreach (var migration in _migrations)
        {
            if (version != migration.FromVersion)
            {
                continue;
            }

            working = migration.MigrateIndexJson(working);
            version = migration.ToVersion;
            migrated = true;
        }

        return (working, migrated);
    }
}
