using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemorySupersedeRequest
{
    public AgentMemorySupersedeRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryId supersededMemoryId,
        AgentMemoryScopeTarget scopeTarget,
        string content,
        AgentMemoryProvenance provenance,
        string idempotencyKey,
        AgentMemoryId? replacementMemoryId = null,
        DateTimeOffset? lastValidatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        WorkspaceKey = workspaceKey;
        SupersededMemoryId = supersededMemoryId;
        ScopeTarget = scopeTarget;
        Content = content;
        Provenance = provenance;
        IdempotencyKey = idempotencyKey;
        ReplacementMemoryId = replacementMemoryId ?? AgentMemoryId.New();
        LastValidatedAtUtc = lastValidatedAtUtc;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentMemoryId SupersededMemoryId { get; }

    public AgentMemoryScopeTarget ScopeTarget { get; }

    public string Content { get; }

    public AgentMemoryProvenance Provenance { get; }

    public string IdempotencyKey { get; }

    public AgentMemoryId ReplacementMemoryId { get; }

    public DateTimeOffset? LastValidatedAtUtc { get; }
}
