using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AgentSessionContinuityInspector : IAgentSessionContinuityInspector
{
    private readonly IAgentDurableRecordStore _store;

    public AgentSessionContinuityInspector(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentSessionContinuityReconcileSummary GetInterruptedSessions(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot,
        int maxRecords = AgentSessionContinuityLimits.DefaultMaxInterruptedSessionsPerPage)
    {
        var replay = _store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.SessionRecovery,
            afterOrderingSequence: 0,
            maxRecords));

        var latestBySession = new Dictionary<string, AgentSessionContinuityCheckpoint>(StringComparer.Ordinal);
        foreach (var envelope in replay.Records.OrderBy(record => record.OrderingSequence))
        {
            if (!AgentSessionContinuityCheckpointSerializer.TryDeserialize(
                    envelope.PayloadJson,
                    out var checkpoint)
                || checkpoint is null)
            {
                continue;
            }

            latestBySession[checkpoint.Scope.SessionId.Value] = checkpoint;
        }

        var interrupted = new List<AgentSessionContinuityInterruptedSession>();
        int recoverable = 0;
        int terminal = 0;
        int indeterminate = 0;

        foreach (var checkpoint in latestBySession.Values)
        {
            if (!string.Equals(
                    checkpoint.Scope.WorkspaceRoot,
                    workspaceRoot,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var classification = checkpoint.Classification;
            var terminated = checkpoint.AcknowledgementState
                is AgentSessionContinuityAcknowledgementState.LocalIntentRecorded
                or AgentSessionContinuityAcknowledgementState.LocalProcessAcknowledged
                or AgentSessionContinuityAcknowledgementState.BackendAcknowledged
                or AgentSessionContinuityAcknowledgementState.BackendAcknowledgementUnavailable
                or AgentSessionContinuityAcknowledgementState.ProviderDeletionUnverified;

            if (terminated && classification == AgentSessionContinuityClassification.Terminal)
            {
                terminal++;
                continue;
            }

            if (classification == AgentSessionContinuityClassification.Recoverable)
            {
                recoverable++;
            }
            else if (classification == AgentSessionContinuityClassification.Indeterminate)
            {
                indeterminate++;
            }
            else
            {
                terminal++;
                continue;
            }

            interrupted.Add(new AgentSessionContinuityInterruptedSession(
                checkpoint.Scope,
                classification,
                checkpoint,
                resumeAdmitted: false,
                terminated: terminated));
        }

        return new AgentSessionContinuityReconcileSummary(
            recoverable,
            terminal,
            indeterminate,
            interrupted);
    }
}
