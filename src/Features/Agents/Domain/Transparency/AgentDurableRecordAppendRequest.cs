using System;

namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Request to append one durable record envelope to a workspace partition.
/// </summary>
internal sealed class AgentDurableRecordAppendRequest
{
    public AgentDurableRecordAppendRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentDurableRecordClass recordClass,
        string idempotencyKey,
        string payloadJson,
        AgentDurableRecordScopeReferences scopeReferences,
        DateTimeOffset? recordedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("Payload is required.", nameof(payloadJson));
        }

        WorkspaceKey = workspaceKey;
        RecordClass = recordClass;
        IdempotencyKey = idempotencyKey;
        PayloadJson = payloadJson;
        ScopeReferences = scopeReferences;
        RecordedAtUtc = recordedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentDurableRecordClass RecordClass { get; }

    public string IdempotencyKey { get; }

    public string PayloadJson { get; }

    public AgentDurableRecordScopeReferences ScopeReferences { get; }

    public DateTimeOffset RecordedAtUtc { get; }
}
