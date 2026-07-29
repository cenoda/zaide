using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityCheckpoint
{
    public AgentSessionContinuityCheckpoint(
        AgentSessionContinuityCheckpointPhase phase,
        AgentSessionContinuityScope scope,
        AgentSessionContinuityClassification classification,
        AgentSessionStatus sessionStatus,
        AgentRunStatus? runStatus,
        int schemaVersion,
        string bindingFingerprint,
        int capabilitySnapshotVersion,
        DateTimeOffset recordedAtUtc,
        string? backendSessionToken = null,
        string? lateCompletionEvidence = null,
        string? disconnectEvidence = null,
        AgentSessionContinuityAcknowledgementState acknowledgementState =
            AgentSessionContinuityAcknowledgementState.None,
        DateTimeOffset? localTerminationIntentAtUtc = null,
        DateTimeOffset? localProcessAcknowledgedAtUtc = null,
        DateTimeOffset? backendAcknowledgedAtUtc = null)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        if (string.IsNullOrWhiteSpace(bindingFingerprint))
        {
            throw new ArgumentException("Binding fingerprint is required.", nameof(bindingFingerprint));
        }

        Phase = phase;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Classification = classification;
        SessionStatus = sessionStatus;
        RunStatus = runStatus;
        SchemaVersion = schemaVersion;
        BindingFingerprint = bindingFingerprint;
        CapabilitySnapshotVersion = capabilitySnapshotVersion;
        RecordedAtUtc = recordedAtUtc;
        BackendSessionToken = backendSessionToken;
        LateCompletionEvidence = lateCompletionEvidence;
        DisconnectEvidence = disconnectEvidence;
        AcknowledgementState = acknowledgementState;
        LocalTerminationIntentAtUtc = localTerminationIntentAtUtc;
        LocalProcessAcknowledgedAtUtc = localProcessAcknowledgedAtUtc;
        BackendAcknowledgedAtUtc = backendAcknowledgedAtUtc;
    }

    public AgentSessionContinuityCheckpointPhase Phase { get; }

    public AgentSessionContinuityScope Scope { get; }

    public AgentSessionContinuityClassification Classification { get; }

    public AgentSessionStatus SessionStatus { get; }

    public AgentRunStatus? RunStatus { get; }

    public int SchemaVersion { get; }

    public string BindingFingerprint { get; }

    public int CapabilitySnapshotVersion { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public string? BackendSessionToken { get; }

    public string? LateCompletionEvidence { get; }

    public string? DisconnectEvidence { get; }

    public AgentSessionContinuityAcknowledgementState AcknowledgementState { get; }

    public DateTimeOffset? LocalTerminationIntentAtUtc { get; }

    public DateTimeOffset? LocalProcessAcknowledgedAtUtc { get; }

    public DateTimeOffset? BackendAcknowledgedAtUtc { get; }
}
