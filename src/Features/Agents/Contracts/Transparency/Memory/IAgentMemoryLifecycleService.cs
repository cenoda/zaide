using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Contracts.Transparency.Memory;

internal interface IAgentMemoryLifecycleService
{
    AgentMemoryExportPackage Export(AgentDurableWorkspaceStorageKey workspaceKey);

    AgentMemoryExportPackage Backup(AgentDurableWorkspaceStorageKey workspaceKey);
}
