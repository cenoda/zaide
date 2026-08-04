namespace Zaide.Features.Agents.Domain.Continuity;

/// <summary>
/// Distinguishes application-start legacy CWD reconciliation from opened-workspace
/// reconciliation. These paths must never silently merge partition ownership.
/// </summary>
internal enum AgentSessionContinuityReconcileOrigin
{
    StartupLegacyCwd = 0,
    WorkspaceOpen = 1,
}
