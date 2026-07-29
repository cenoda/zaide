using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Contracts.Transparency.Memory;

internal interface IAgentMemoryRetrievalService
{
    AgentMemoryRetrievalResult Retrieve(AgentMemoryRetrievalRequest request);
}
