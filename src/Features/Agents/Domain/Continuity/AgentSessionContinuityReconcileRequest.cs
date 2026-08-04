using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityReconcileRequest
{
    public AgentSessionContinuityReconcileRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot,
        bool isStartup = false,
        AgentSessionContinuityReconcileOrigin origin = AgentSessionContinuityReconcileOrigin.WorkspaceOpen)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        WorkspaceKey = workspaceKey;
        WorkspaceRoot = workspaceRoot;
        IsStartup = isStartup;
        Origin = origin;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string WorkspaceRoot { get; }

    public bool IsStartup { get; }

    public AgentSessionContinuityReconcileOrigin Origin { get; }
}
