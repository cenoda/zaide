namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Deterministic permission classification for one immutable action request.
/// </summary>
internal enum AgentActionPermissionClassification
{
    DeniedByPolicy,
    RequiresUserDecision,
    AllowedByLockedPolicy,
}
