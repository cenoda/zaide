namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Workspace partition load outcome.
/// </summary>
internal enum AgentDurableRecordLoadOutcome
{
    Missing = 0,
    Loaded = 1,
    Migrated = 2,
    Corrupt = 3,
    UnsupportedVersion = 4,
    Quarantined = 5,
}
