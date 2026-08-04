using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application.Continuity;

internal static class AgentSessionContinuityCheckpointSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(AgentSessionContinuityCheckpoint checkpoint) =>
        JsonSerializer.Serialize(ToPayload(checkpoint), Options);

    public static bool TryDeserialize(
        string payloadJson,
        out AgentSessionContinuityCheckpoint? checkpoint)
    {
        checkpoint = null;
        try
        {
            var payload = JsonSerializer.Deserialize<CheckpointPayload>(payloadJson, Options);
            if (payload is null || payload.SchemaVersion != AgentSessionContinuityLimits.PayloadSchemaVersion)
            {
                return false;
            }

            var scope = new AgentSessionContinuityScope(
                ActorId.FromValue(payload.ActorId),
                ConversationId.FromValue(payload.ConversationId),
                AgentSessionId.FromValue(payload.SessionId),
                string.IsNullOrWhiteSpace(payload.RunId)
                    ? null
                    : ExecutionRunId.FromValue(payload.RunId),
                AgentBackendId.FromValue(payload.BackendId),
                AgentDurableWorkspaceStorageKey.FromValue(payload.WorkspaceKey),
                payload.WorkspaceRoot);

            checkpoint = new AgentSessionContinuityCheckpoint(
                payload.Phase,
                scope,
                payload.Classification,
                payload.SessionStatus,
                payload.RunStatus,
                payload.SchemaVersion,
                payload.BindingFingerprint,
                payload.CapabilitySnapshotVersion,
                payload.RecordedAtUtc,
                payload.BackendSessionToken,
                payload.LateCompletionEvidence,
                payload.DisconnectEvidence,
                payload.AcknowledgementState,
                payload.LocalTerminationIntentAtUtc,
                payload.LocalProcessAcknowledgedAtUtc,
                payload.BackendAcknowledgedAtUtc);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static CheckpointPayload ToPayload(AgentSessionContinuityCheckpoint checkpoint) =>
        new()
        {
            SchemaVersion = checkpoint.SchemaVersion,
            Operation = checkpoint.Phase
                is AgentSessionContinuityCheckpointPhase.AfterStartupReconcile
                or AgentSessionContinuityCheckpointPhase.AfterWorkspaceOpenReconcile
                ? AgentSessionContinuityOperationKind.Reconcile
                : AgentSessionContinuityOperationKind.Checkpoint,
            Phase = checkpoint.Phase,
            Classification = checkpoint.Classification,
            ActorId = checkpoint.Scope.ActorId.Value,
            ConversationId = checkpoint.Scope.ConversationId.Value,
            SessionId = checkpoint.Scope.SessionId.Value,
            RunId = checkpoint.Scope.RunId?.Value,
            BackendId = checkpoint.Scope.BackendId.Value,
            WorkspaceKey = checkpoint.Scope.WorkspaceKey.Value,
            WorkspaceRoot = checkpoint.Scope.WorkspaceRoot,
            SessionStatus = checkpoint.SessionStatus,
            RunStatus = checkpoint.RunStatus,
            BindingFingerprint = checkpoint.BindingFingerprint,
            CapabilitySnapshotVersion = checkpoint.CapabilitySnapshotVersion,
            BackendSessionToken = checkpoint.BackendSessionToken,
            LateCompletionEvidence = checkpoint.LateCompletionEvidence,
            DisconnectEvidence = checkpoint.DisconnectEvidence,
            AcknowledgementState = checkpoint.AcknowledgementState,
            LocalTerminationIntentAtUtc = checkpoint.LocalTerminationIntentAtUtc,
            LocalProcessAcknowledgedAtUtc = checkpoint.LocalProcessAcknowledgedAtUtc,
            BackendAcknowledgedAtUtc = checkpoint.BackendAcknowledgedAtUtc,
            RecordedAtUtc = checkpoint.RecordedAtUtc,
        };

    internal sealed class CheckpointPayload
    {
        public int SchemaVersion { get; set; }

        public AgentSessionContinuityOperationKind Operation { get; set; }

        public AgentSessionContinuityCheckpointPhase Phase { get; set; }

        public AgentSessionContinuityClassification Classification { get; set; }

        public string ActorId { get; set; } = string.Empty;

        public string ConversationId { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public string? RunId { get; set; }

        public string BackendId { get; set; } = string.Empty;

        public string WorkspaceKey { get; set; } = string.Empty;

        public string WorkspaceRoot { get; set; } = string.Empty;

        public AgentSessionStatus SessionStatus { get; set; }

        public AgentRunStatus? RunStatus { get; set; }

        public string BindingFingerprint { get; set; } = string.Empty;

        public int CapabilitySnapshotVersion { get; set; }

        public string? BackendSessionToken { get; set; }

        public string? LateCompletionEvidence { get; set; }

        public string? DisconnectEvidence { get; set; }

        public AgentSessionContinuityAcknowledgementState AcknowledgementState { get; set; }

        public DateTimeOffset? LocalTerminationIntentAtUtc { get; set; }

        public DateTimeOffset? LocalProcessAcknowledgedAtUtc { get; set; }

        public DateTimeOffset? BackendAcknowledgedAtUtc { get; set; }

        public DateTimeOffset RecordedAtUtc { get; set; }
    }
}

internal static class AgentSessionContinuityBindingFingerprint
{
    public static string Compute(
        ActorId actorId,
        AgentBackendId backendId,
        string workspaceRoot,
        string? runtimeExecutable = null,
        string? expectedAgentName = null,
        string? expectedAgentVersion = null) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    string.Join(
                        "|",
                        actorId.Value,
                        backendId.Value,
                        workspaceRoot,
                        runtimeExecutable ?? string.Empty,
                        expectedAgentName ?? string.Empty,
                        expectedAgentVersion ?? string.Empty))));
}
