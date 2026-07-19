using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Resolved route request with typed target identity after mention parsing.
/// M4 — zero or one explicit mention target supported.
/// </summary>
public sealed record RouteRequest(
    string SourcePanelId,
    ActorId TargetActorId,
    string TargetPanelId,
    ConversationId ConversationId,
    string ContentAfterStrip,
    bool IsDirectSend);
