using System;
using System.Linq;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AcpAgentContinuityAdapter : IAgentBackendContinuityAdapter
{
    public AgentBackendId BackendId => AgentBackendIds.Acp;

    public AgentBackendContinuityCapabilityRow GetCapabilityRow() =>
        AgentBackendContinuityCapabilityMatrix.Rows
            .First(row => row.BackendId == AgentBackendIds.AcpValue);

    public AgentBackendContinuityProbeResult ProbeBackendSession(
        AgentBackendContinuityProbeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AgentBackendContinuityProbeResult(
            backendReachable: false,
            sessionTokenValid: false,
            acknowledgementState: AgentSessionContinuityAcknowledgementState.BackendAcknowledgementUnavailable,
            evidenceNote:
                "ACP session/resume is unavailable in the accepted Phase 20 profile. No provider deletion claim is made.");
    }
}
