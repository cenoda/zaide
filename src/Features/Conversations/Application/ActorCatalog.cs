using System;
using System.Collections.Generic;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Application;

/// <summary>
/// Application-lifetime in-memory actor catalog. Owns canonical seeds and dynamic
/// panel fallback/custom registrations.
/// </summary>
internal sealed class ActorCatalog : IActorCatalog
{
    private readonly object _sync = new();
    private readonly Dictionary<ActorId, Actor> _actors = new();

    public ActorCatalog()
    {
        foreach (var seed in CanonicalActorSeeds.All)
        {
            _actors[seed.Id] = seed;
        }
    }

    public Actor CanonicalHuman => _actors[ActorId.HumanUser];

    public Actor CanonicalTownhallAgent => _actors[ActorId.TownhallAgent];

    public Actor GetPanelSeedActor(int seedIndex)
    {
        if (seedIndex < 0 || seedIndex >= CanonicalActorSeeds.PanelSeedActors.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seedIndex),
                seedIndex,
                "Panel seed index must be in the fixed alpha..delta range.");
        }

        return CanonicalActorSeeds.PanelSeedActors[seedIndex];
    }

    public Actor GetOrRegisterPanelFallbackActor(int fallbackNumber)
    {
        var id = ActorId.PanelFallback(fallbackNumber);
        lock (_sync)
        {
            if (_actors.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var actor = new Actor(
                id,
                ActorKind.Agent,
                ProjectedLegacyId: $"agent-{fallbackNumber}",
                DisplayName: $"Agent {fallbackNumber}",
                AvatarResourceKey: "Icon.Avatar");
            _actors[id] = actor;
            return actor;
        }
    }

    public int PanelSeedCount => CanonicalActorSeeds.PanelSeedActors.Count;

    public Actor RegisterOrGetCustomPanelActor(
        string legacyAgentId,
        string displayName,
        string avatarResourceKey)
    {
        var id = ActorId.PanelCustom(legacyAgentId);
        lock (_sync)
        {
            if (_actors.TryGetValue(id, out var existing))
            {
                if (existing.Kind != ActorKind.Agent
                    || !string.Equals(existing.ProjectedLegacyId, legacyAgentId, StringComparison.Ordinal)
                    || !string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal)
                    || !string.Equals(existing.AvatarResourceKey, avatarResourceKey, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Custom panel actor '{legacyAgentId}' is already registered with conflicting identity data.",
                        nameof(legacyAgentId));
                }

                return existing;
            }

            var actor = new Actor(
                id,
                ActorKind.Agent,
                ProjectedLegacyId: legacyAgentId,
                DisplayName: displayName,
                AvatarResourceKey: avatarResourceKey);
            _actors[id] = actor;
            return actor;
        }
    }

    public bool TryGet(ActorId id, out Actor actor)
    {
        lock (_sync)
        {
            return _actors.TryGetValue(id, out actor!);
        }
    }
}
