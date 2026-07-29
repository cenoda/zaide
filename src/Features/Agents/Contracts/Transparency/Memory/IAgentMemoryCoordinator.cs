using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Contracts.Transparency.Memory;

internal interface IAgentMemoryCoordinator
{
    AgentMemoryOperationResult Create(AgentMemoryCreateRequest request);

    AgentMemoryOperationResult Correct(AgentMemoryCorrectRequest request);

    AgentMemoryOperationResult Disable(AgentMemoryDisableRequest request);

    AgentMemoryOperationResult Supersede(AgentMemorySupersedeRequest request);

    AgentMemoryOperationResult Delete(AgentMemoryDeleteRequest request);
}
