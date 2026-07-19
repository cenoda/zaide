using System;
using System.Collections.Generic;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Application;

/// <summary>
/// Locked canonical seed table for Refactor 7 M1. Values must match the accepted
/// IMPLEMENTATION_PLAN identity policy and preserve projected legacy strings.
/// </summary>
internal static class CanonicalActorSeeds
{
    internal static readonly IReadOnlyList<Actor> All =
    [
        new(
            ActorId.HumanUser,
            ActorKind.Human,
            ProjectedLegacyId: "user-1",
            DisplayName: "User",
            AvatarResourceKey: "avatar-user"),
        new(
            ActorId.TownhallAgent,
            ActorKind.Agent,
            ProjectedLegacyId: "agent-1",
            DisplayName: "Zaide Agent",
            AvatarResourceKey: "avatar-agent"),
        new(
            ActorId.PanelSeed("alpha"),
            ActorKind.Agent,
            ProjectedLegacyId: "alpha",
            DisplayName: "Alpha",
            AvatarResourceKey: "Icon.Avatar"),
        new(
            ActorId.PanelSeed("beta"),
            ActorKind.Agent,
            ProjectedLegacyId: "beta",
            DisplayName: "Beta",
            AvatarResourceKey: "Icon.Avatar"),
        new(
            ActorId.PanelSeed("gamma"),
            ActorKind.Agent,
            ProjectedLegacyId: "gamma",
            DisplayName: "Gamma",
            AvatarResourceKey: "Icon.Avatar"),
        new(
            ActorId.PanelSeed("delta"),
            ActorKind.Agent,
            ProjectedLegacyId: "delta",
            DisplayName: "Delta",
            AvatarResourceKey: "Icon.Avatar"),
    ];

    internal static readonly IReadOnlyList<Actor> PanelSeedActors =
    [
        All[2],
        All[3],
        All[4],
        All[5],
    ];
}
