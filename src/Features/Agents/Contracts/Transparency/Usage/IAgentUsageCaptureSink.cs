using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Contracts.Transparency.Usage;

internal interface IAgentUsageCaptureSink
{
    AgentUsageCaptureResult TrySubmit(AgentUsageCaptureRequest request);
}
