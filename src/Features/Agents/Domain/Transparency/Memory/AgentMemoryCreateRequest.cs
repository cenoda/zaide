using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryCreateRequest
{
    public AgentMemoryCreateRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryScopeTarget scopeTarget,
        string content,
        AgentMemoryProvenance provenance,
        string idempotencyKey,
        DateTimeOffset? lastValidatedAtUtc = null,
        AgentMemoryId? memoryId = null)
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
        ScopeTarget = scopeTarget;
        Content = content;
        Provenance = provenance;
        IdempotencyKey = idempotencyKey;
        LastValidatedAtUtc = lastValidatedAtUtc;
        MemoryId = memoryId ?? AgentMemoryId.New();
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentMemoryScopeTarget ScopeTarget { get; }

    public string Content { get; }

    public AgentMemoryProvenance Provenance { get; }

    public string IdempotencyKey { get; }

    public DateTimeOffset? LastValidatedAtUtc { get; }

    public AgentMemoryId MemoryId { get; }
}
