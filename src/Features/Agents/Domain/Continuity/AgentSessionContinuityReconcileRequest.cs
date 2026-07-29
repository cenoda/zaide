using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityReconcileRequest
{
    public AgentSessionContinuityReconcileRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot,
        bool isStartup = false)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        WorkspaceKey = workspaceKey;
        WorkspaceRoot = workspaceRoot;
        IsStartup = isStartup;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string WorkspaceRoot { get; }

    public bool IsStartup { get; }
}
