using System;

namespace Zaide.Features.Agents.Domain.Transparency;

internal sealed class AgentTransparencyRestoreResult
{
    public AgentTransparencyRestoreResult(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentTransparencyLifecycleStatus status,
        AgentDurableRecordLoadOutcome loadOutcome,
        string? reason = null)
    {
        WorkspaceKey = workspaceKey;
        Status = status;
        LoadOutcome = loadOutcome;
        Reason = reason;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentTransparencyLifecycleStatus Status { get; }

    public AgentDurableRecordLoadOutcome LoadOutcome { get; }

    public string? Reason { get; }
}
