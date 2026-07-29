using System.Collections.Generic;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentBackendContinuityCapabilityRow
{
    public AgentBackendContinuityCapabilityRow(
        string backendId,
        bool checkpointSupported,
        bool resumeCurrentlyUsable,
        bool terminateAckSupported,
        bool reconnectSupported,
        string evidenceNote)
    {
        BackendId = backendId;
        CheckpointSupported = checkpointSupported;
        ResumeCurrentlyUsable = resumeCurrentlyUsable;
        TerminateAckSupported = terminateAckSupported;
        ReconnectSupported = reconnectSupported;
        EvidenceNote = evidenceNote;
    }

    public string BackendId { get; }

    public bool CheckpointSupported { get; }

    public bool ResumeCurrentlyUsable { get; }

    public bool TerminateAckSupported { get; }

    public bool ReconnectSupported { get; }

    public string EvidenceNote { get; }
}

internal static class AgentBackendContinuityCapabilityMatrix
{
    public static IReadOnlyList<AgentBackendContinuityCapabilityRow> Rows { get; } =
        new[]
        {
            new AgentBackendContinuityCapabilityRow(
                AgentBackendIds.NativeHarnessValue,
                checkpointSupported: true,
                resumeCurrentlyUsable: false,
                terminateAckSupported: false,
                reconnectSupported: false,
                evidenceNote:
                    "Native Harness exposes Zaide-owned checkpoints only. Backend session resume is unavailable."),
            new AgentBackendContinuityCapabilityRow(
                AgentBackendIds.AcpValue,
                checkpointSupported: true,
                resumeCurrentlyUsable: false,
                terminateAckSupported: false,
                reconnectSupported: false,
                evidenceNote:
                    "ACP Phase 20 profile marks session/resume unavailable. Continuity records opaque evidence only."),
        };
}
