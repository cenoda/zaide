using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Presentation row for a direct conversation in the Townhall sidebar.
/// </summary>
internal sealed class TownhallNavigationItem
{
    public required ConversationId ConversationId { get; init; }

    public required TownhallNavigationKind Kind { get; init; }

    public required string Label { get; init; }

    public ActorId? PeerActorId { get; init; }

    public bool IsSelected { get; set; }
}
