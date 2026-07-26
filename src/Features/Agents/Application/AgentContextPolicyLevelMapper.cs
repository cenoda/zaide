using System;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

internal static class AgentContextPolicyLevelMapper
{
    public static AgentSessionContextPolicyLevel ToContract(AgentContextPolicyLevel level) =>
        level switch
        {
            AgentContextPolicyLevel.Off => AgentSessionContextPolicyLevel.Off,
            AgentContextPolicyLevel.Minimal => AgentSessionContextPolicyLevel.Minimal,
            AgentContextPolicyLevel.Standard => AgentSessionContextPolicyLevel.Standard,
            AgentContextPolicyLevel.Detailed => AgentSessionContextPolicyLevel.Detailed,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Policy level is invalid."),
        };

    public static AgentContextPolicyLevel ToDomain(AgentSessionContextPolicyLevel level) =>
        level switch
        {
            AgentSessionContextPolicyLevel.Off => AgentContextPolicyLevel.Off,
            AgentSessionContextPolicyLevel.Minimal => AgentContextPolicyLevel.Minimal,
            AgentSessionContextPolicyLevel.Standard => AgentContextPolicyLevel.Standard,
            AgentSessionContextPolicyLevel.Detailed => AgentContextPolicyLevel.Detailed,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Policy level is invalid."),
        };
}
