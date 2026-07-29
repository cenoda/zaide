using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Contracts.Transparency.Usage;

internal interface IAgentUsageBackendEvidenceSource
{
    string BackendId { get; }

    bool CanExpose(AgentUsageKind kind);

    AgentUsageCaptureResult Submit(AgentUsageCaptureRequest request);
}
