using System;

namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Versioned backend-neutral durable record envelope. Payload remains opaque at
/// M1; later milestones attach class-specific typed payloads.
/// </summary>
internal sealed class AgentDurableRecordEnvelope
{
    public AgentDurableRecordEnvelope(
        int schemaVersion,
        AgentDurableRecordId recordId,
        AgentDurableRecordClass recordClass,
        AgentDurableWorkspaceStorageKey workspaceKey,
        long orderingSequence,
        string idempotencyKey,
        DateTimeOffset recordedAtUtc,
        AgentDurableRecordScopeReferences scopeReferences,
        string payloadJson)
    {
        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("Payload is required.", nameof(payloadJson));
        }

        SchemaVersion = schemaVersion;
        RecordId = recordId;
        RecordClass = recordClass;
        WorkspaceKey = workspaceKey;
        OrderingSequence = orderingSequence;
        IdempotencyKey = idempotencyKey;
        RecordedAtUtc = recordedAtUtc;
        ScopeReferences = scopeReferences;
        PayloadJson = payloadJson;
    }

    public int SchemaVersion { get; }

    public AgentDurableRecordId RecordId { get; }

    public AgentDurableRecordClass RecordClass { get; }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public long OrderingSequence { get; }

    public string IdempotencyKey { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public AgentDurableRecordScopeReferences ScopeReferences { get; }

    public string PayloadJson { get; }
}
