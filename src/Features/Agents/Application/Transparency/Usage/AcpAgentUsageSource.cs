using System;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Application.Transparency.Usage;

internal sealed class AcpAgentUsageSource : IAgentUsageBackendEvidenceSource
{
    private readonly AgentUsageBackendEvidenceSourceWriter _writer;

    public AcpAgentUsageSource(AgentUsageBackendEvidenceSourceWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public string BackendId => AgentBackendIds.AcpValue;

    public bool CanExpose(AgentUsageKind kind) => kind switch
    {
        AgentUsageKind.TokensInput => true,
        AgentUsageKind.TokensOutput => true,
        AgentUsageKind.TotalTokens => true,
        AgentUsageKind.EstimatedCost => true,
        AgentUsageKind.TotalCost => true,
        AgentUsageKind.RequestCount => true,
        AgentUsageKind.LatencyMs => true,
        _ => false,
    };

    public AgentUsageCaptureResult Submit(AgentUsageCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanExpose(request.Kind))
        {
            return new AgentUsageCaptureResult(
                AgentUsageCaptureStatus.InvalidRequest,
                reason: "acp-cannot-expose-kind");
        }

        return _writer.Submit(request);
    }
}
