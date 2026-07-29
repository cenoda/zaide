using System;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal sealed class AgentMemoryLifecycleService : IAgentMemoryLifecycleService
{
    private readonly AgentMemoryInspector _inspector;

    public AgentMemoryLifecycleService(AgentMemoryInspector inspector)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
    }

    public AgentMemoryExportPackage Export(AgentDurableWorkspaceStorageKey workspaceKey) =>
        BuildPackage(workspaceKey, partialUnavailable: false);

    public AgentMemoryExportPackage Backup(AgentDurableWorkspaceStorageKey workspaceKey) =>
        BuildPackage(workspaceKey, partialUnavailable: false);

    private AgentMemoryExportPackage BuildPackage(
        AgentDurableWorkspaceStorageKey workspaceKey,
        bool partialUnavailable)
    {
        var records = _inspector.ReplayAll(workspaceKey, includeDeleted: true);
        return new AgentMemoryExportPackage(
            workspaceKey,
            AgentMemoryLimits.PayloadSchemaVersion,
            DateTimeOffset.UtcNow,
            records,
            partialUnavailable);
    }
}
