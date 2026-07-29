using System;
using System.Security.Cryptography;
using System.Text;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AgentSessionContinuityCheckpointWriter
{
    private readonly IAgentDurableRecordStore _store;

    public AgentSessionContinuityCheckpointWriter(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentSessionContinuityOperationResult TryWrite(
        AgentSessionContinuityCheckpoint checkpoint,
        AgentSessionContinuityOperationKind operation,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var payloadJson = AgentSessionContinuityCheckpointSerializer.Serialize(checkpoint);
        var appendRequest = new AgentDurableRecordAppendRequest(
            checkpoint.Scope.WorkspaceKey,
            AgentDurableRecordClass.SessionRecovery,
            idempotencyKey,
            payloadJson,
            new AgentDurableRecordScopeReferences(
                conversationId: checkpoint.Scope.ConversationId.Value,
                sessionId: checkpoint.Scope.SessionId.Value,
                runId: checkpoint.Scope.RunId?.Value,
                backendId: checkpoint.Scope.BackendId.Value),
            recordedAtUtc: checkpoint.RecordedAtUtc);

        var result = _store.TryAppend(appendRequest);
        if (result.Status == AgentDurableRecordAppendStatus.DuplicateIgnored)
        {
            return new AgentSessionContinuityOperationResult(
                AgentSessionContinuityOperationStatus.DuplicateIgnored,
                operation,
                checkpoint.Classification,
                checkpoint.AcknowledgementState,
                reason: "Idempotent duplicate ignored.",
                orderingSequence: result.Envelope?.OrderingSequence);
        }

        if (result.Status != AgentDurableRecordAppendStatus.Appended)
        {
            return new AgentSessionContinuityOperationResult(
                AgentSessionContinuityOperationStatus.Rejected,
                operation,
                checkpoint.Classification,
                checkpoint.AcknowledgementState,
                reason: $"Store rejected append: {result.Status}");
        }

        return new AgentSessionContinuityOperationResult(
            AgentSessionContinuityOperationStatus.Accepted,
            operation,
            checkpoint.Classification,
            checkpoint.AcknowledgementState,
            orderingSequence: result.Envelope?.OrderingSequence);
    }

    public static string BuildCheckpointIdempotencyKey(
        AgentSessionContinuityCheckpointPhase phase,
        AgentSessionContinuityScope scope) =>
        "continuity:checkpoint:" +
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    string.Join(
                        "|",
                        phase.ToString(),
                        scope.SessionId.Value,
                        scope.RunId?.Value ?? string.Empty,
                        scope.WorkspaceKey.Value))))[..24];
}
