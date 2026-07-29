using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Contracts.Continuity;

internal interface IAgentBackendContinuityAdapter
{
    AgentBackendId BackendId { get; }

    AgentBackendContinuityCapabilityRow GetCapabilityRow();

    AgentBackendContinuityProbeResult ProbeBackendSession(AgentBackendContinuityProbeRequest request);
}
