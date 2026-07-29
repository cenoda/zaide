using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryRetrievalRequest
{
    public AgentMemoryRetrievalRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryRetrievalContext context)
    {
        if (workspaceKey.Value.Length == 0)
        {
            throw new ArgumentException("Workspace key is required.", nameof(workspaceKey));
        }

        ArgumentNullException.ThrowIfNull(context);

        WorkspaceKey = workspaceKey;
        Context = context;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentMemoryRetrievalContext Context { get; }
}
