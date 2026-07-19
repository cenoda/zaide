namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Canonical actor identity row with legacy projection fields for current UI surfaces.
/// </summary>
public sealed record Actor(
    ActorId Id,
    ActorKind Kind,
    string ProjectedLegacyId,
    string DisplayName,
    string AvatarResourceKey);
