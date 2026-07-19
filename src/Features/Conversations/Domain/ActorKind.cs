namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Stable actor classification for conversation participants.
/// Refactor 7 M1 — intentionally minimal (Human and Agent only).
/// </summary>
public enum ActorKind
{
    Human,
    Agent,
}
