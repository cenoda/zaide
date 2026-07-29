using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Provenance for one memory record revision.
/// </summary>
internal sealed class AgentMemoryProvenance
{
    public AgentMemoryProvenance(
        ActorId authorActorId,
        string sourceRevision,
        AgentMemorySourceKind sourceKind,
        string? sourceDescription = null)
    {
        if (authorActorId == default)
        {
            throw new ArgumentException("Author actor id is required.", nameof(authorActorId));
        }

        if (string.IsNullOrWhiteSpace(sourceRevision))
        {
            throw new ArgumentException("Source revision is required.", nameof(sourceRevision));
        }

        AuthorActorId = authorActorId;
        SourceRevision = sourceRevision;
        SourceKind = sourceKind;
        SourceDescription = sourceDescription;
    }

    public ActorId AuthorActorId { get; }

    public string SourceRevision { get; }

    public AgentMemorySourceKind SourceKind { get; }

    public string? SourceDescription { get; }
}
