using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Contracts.Continuity;

internal sealed class AgentBackendContinuityProbeRequest
{
    public AgentBackendContinuityProbeRequest(
        string? backendSessionToken,
        string bindingFingerprint)
    {
        BackendSessionToken = backendSessionToken;
        BindingFingerprint = bindingFingerprint ?? string.Empty;
    }

    public string? BackendSessionToken { get; }

    public string BindingFingerprint { get; }
}

internal sealed class AgentBackendContinuityProbeResult
{
    public AgentBackendContinuityProbeResult(
        bool backendReachable,
        bool sessionTokenValid,
        AgentSessionContinuityAcknowledgementState acknowledgementState,
        string? evidenceNote = null)
    {
        BackendReachable = backendReachable;
        SessionTokenValid = sessionTokenValid;
        AcknowledgementState = acknowledgementState;
        EvidenceNote = evidenceNote;
    }

    public bool BackendReachable { get; }

    public bool SessionTokenValid { get; }

    public AgentSessionContinuityAcknowledgementState AcknowledgementState { get; }

    public string? EvidenceNote { get; }
}
