using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryDeleteRequest
{
    public AgentMemoryDeleteRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryId memoryId,
        AgentMemoryProvenance provenance,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        WorkspaceKey = workspaceKey;
        MemoryId = memoryId;
        Provenance = provenance;
        IdempotencyKey = idempotencyKey;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentMemoryId MemoryId { get; }

    public AgentMemoryProvenance Provenance { get; }

    public string IdempotencyKey { get; }
}
