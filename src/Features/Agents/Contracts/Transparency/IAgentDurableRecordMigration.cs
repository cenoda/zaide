namespace Zaide.Features.Agents.Contracts.Transparency;

/// <summary>
/// Ordered partition-index migration step.
/// </summary>
internal interface IAgentDurableRecordMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    string MigrateIndexJson(string indexJson);
}
