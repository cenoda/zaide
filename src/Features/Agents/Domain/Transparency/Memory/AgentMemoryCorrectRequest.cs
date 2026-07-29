using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryCorrectRequest
{
    public AgentMemoryCorrectRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryId memoryId,
        string content,
        AgentMemoryProvenance provenance,
        string idempotencyKey,
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
        MemoryId = memoryId;
        Content = content;
        Provenance = provenance;
        IdempotencyKey = idempotencyKey;
        LastValidatedAtUtc = lastValidatedAtUtc;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentMemoryId MemoryId { get; }

    public string Content { get; }

    public AgentMemoryProvenance Provenance { get; }

    public string IdempotencyKey { get; }

    public DateTimeOffset? LastValidatedAtUtc { get; }
}
