using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Contracts;

/// <summary>
/// Authoritative in-memory actor catalog for current seeded and dynamic panel identities.
/// </summary>
public interface IActorCatalog
{
    Actor CanonicalHuman { get; }

    Actor CanonicalTownhallAgent { get; }

    Actor GetPanelSeedActor(int seedIndex);

    Actor GetOrRegisterPanelFallbackActor(int fallbackNumber);

    int PanelSeedCount { get; }

    Actor RegisterOrGetCustomPanelActor(
        string legacyAgentId,
        string displayName,
        string avatarResourceKey);

    bool TryGet(ActorId id, out Actor actor);

    bool TryGetByProjectedLegacyId(string projectedLegacyId, out Actor actor);
}
