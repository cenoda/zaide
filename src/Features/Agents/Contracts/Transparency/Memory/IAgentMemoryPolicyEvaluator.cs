using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Contracts.Transparency.Memory;

internal interface IAgentMemoryPolicyEvaluator
{
    AgentMemoryPolicyEvaluation EvaluateCreate(
        AgentMemoryCreateRequest request,
        IReadOnlyList<AgentMemoryRecord> existingRecords);

    AgentMemoryPolicyEvaluation EvaluateCorrect(
        AgentMemoryCorrectRequest request,
        AgentMemoryRecord existing);

    AgentMemoryPolicyEvaluation EvaluateSupersede(
        AgentMemorySupersedeRequest request,
        AgentMemoryRecord superseded);
}
