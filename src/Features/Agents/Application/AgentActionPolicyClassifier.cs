using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Deterministic permission classifier for Phase 17 action requests.
/// </summary>
internal static class AgentActionPolicyClassifier
{
    public static AgentActionPermissionClassification Classify(AgentActionPayload payload)
    {
        return payload.Kind switch
        {
            AgentActionKind.ReadFile => AgentActionPermissionClassification.AllowedByLockedPolicy,
            AgentActionKind.CreateFile => AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionKind.ReplaceFile => AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionKind.DeleteFile => AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionKind.ExecuteCommand => AgentActionPermissionClassification.RequiresUserDecision,
            _ => AgentActionPermissionClassification.DeniedByPolicy,
        };
    }
}
