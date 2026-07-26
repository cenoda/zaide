namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Application-wide default IDE context disclosure policy.
/// </summary>
internal static class AgentContextApplicationDefault
{
    public static AgentContextPolicyLevel Level => AgentContextPolicyLevel.Standard;
}
