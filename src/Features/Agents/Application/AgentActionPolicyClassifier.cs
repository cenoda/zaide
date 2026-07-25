using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Deterministic permission classifier for Phase 17 action requests.
/// </summary>
internal static class AgentActionPolicyClassifier
{
    public static AgentActionPermissionClassification Classify(
        AgentActionPayload payload,
        AgentResolvedCommand? resolvedCommand = null)
    {
        if (payload is AgentExecuteCommandActionPayload
            && resolvedCommand?.DenylistResult.IsDenied == true)
        {
            return AgentActionPermissionClassification.DeniedByPolicy;
        }

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
