using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Contracts.Transparency;

internal interface IAgentTransparencyLifecycleCoordinator
{
    AgentTransparencyExportPackage Export(AgentDurableWorkspaceStorageKey workspaceKey);

    AgentTransparencyBackupPackage Backup(AgentDurableWorkspaceStorageKey workspaceKey);

    AgentTransparencyRestoreResult Restore(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backupDirectory);

    AgentDurableRecordLoadOutcome Migrate(AgentDurableWorkspaceStorageKey workspaceKey);
}
