using System;
using System.Linq;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class NativeHarnessAgentContinuityAdapter : IAgentBackendContinuityAdapter
{
    public AgentBackendId BackendId => AgentBackendIds.NativeHarness;

    public AgentBackendContinuityCapabilityRow GetCapabilityRow() =>
        AgentBackendContinuityCapabilityMatrix.Rows
            .First(row => row.BackendId == AgentBackendIds.NativeHarnessValue);

    public AgentBackendContinuityProbeResult ProbeBackendSession(
        AgentBackendContinuityProbeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AgentBackendContinuityProbeResult(
            backendReachable: true,
            sessionTokenValid: false,
            acknowledgementState: AgentSessionContinuityAcknowledgementState.BackendAcknowledgementUnavailable,
            evidenceNote:
                "Native Harness does not expose a resumable backend session token. Zaide-owned checkpoints only.");
    }
}
