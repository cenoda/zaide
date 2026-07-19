using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Application;

public sealed class ActorCatalogTests
{
    [Fact]
    public void CanonicalSeeds_ExposeLockedHumanTownhallAndPanelRows()
    {
        var catalog = new ActorCatalog();

        Assert.Equal("human:user-1", catalog.CanonicalHuman.Id.Value);
        Assert.Equal(ActorKind.Human, catalog.CanonicalHuman.Kind);
        Assert.Equal("user-1", catalog.CanonicalHuman.ProjectedLegacyId);
        Assert.Equal("User", catalog.CanonicalHuman.DisplayName);
        Assert.Equal("avatar-user", catalog.CanonicalHuman.AvatarResourceKey);

        Assert.Equal("townhall-agent:agent-1", catalog.CanonicalTownhallAgent.Id.Value);
        Assert.Equal(ActorKind.Agent, catalog.CanonicalTownhallAgent.Kind);
        Assert.Equal("agent-1", catalog.CanonicalTownhallAgent.ProjectedLegacyId);
        Assert.Equal("Zaide Agent", catalog.CanonicalTownhallAgent.DisplayName);
        Assert.Equal("avatar-agent", catalog.CanonicalTownhallAgent.AvatarResourceKey);

        Assert.Equal(4, catalog.PanelSeedCount);
        Assert.Equal("alpha", catalog.GetPanelSeedActor(0).ProjectedLegacyId);
        Assert.Equal("Alpha", catalog.GetPanelSeedActor(0).DisplayName);
        Assert.Equal("Icon.Avatar", catalog.GetPanelSeedActor(0).AvatarResourceKey);
        Assert.Equal("panel-seed:beta", catalog.GetPanelSeedActor(1).Id.Value);
        Assert.Equal("gamma", catalog.GetPanelSeedActor(2).ProjectedLegacyId);
        Assert.Equal("delta", catalog.GetPanelSeedActor(3).ProjectedLegacyId);
    }

    [Fact]
    public void RegisterOrGetCustomPanelActor_RegistersAgentX()
    {
        var catalog = new ActorCatalog();

        var actor = catalog.RegisterOrGetCustomPanelActor("agent-x", "X Agent", "avatar_x");

        Assert.Equal("panel-custom:agent-x", actor.Id.Value);
        Assert.Equal("agent-x", actor.ProjectedLegacyId);
        Assert.Equal("X Agent", actor.DisplayName);
        Assert.Equal("avatar_x", actor.AvatarResourceKey);
    }

    [Fact]
    public void RegisterOrGetCustomPanelActor_ReusesIdenticalRegistration()
    {
        var catalog = new ActorCatalog();

        var first = catalog.RegisterOrGetCustomPanelActor("agent-x", "X Agent", "avatar_x");
        var second = catalog.RegisterOrGetCustomPanelActor("agent-x", "X Agent", "avatar_x");

        Assert.Same(first, second);
    }

    [Fact]
    public void RegisterOrGetCustomPanelActor_ConflictingRegistration_ThrowsBeforeMutation()
    {
        var catalog = new ActorCatalog();
        catalog.RegisterOrGetCustomPanelActor("agent-x", "X Agent", "avatar_x");

        var ex = Assert.Throws<ArgumentException>(() =>
            catalog.RegisterOrGetCustomPanelActor("agent-x", "Different", "avatar_x"));

        Assert.Contains("conflicting identity", ex.Message, StringComparison.Ordinal);
        Assert.True(catalog.TryGet(ActorId.PanelCustom("agent-x"), out var actor));
        Assert.Equal("X Agent", actor!.DisplayName);
    }

    [Fact]
    public void CustomLegacyIds_AreIsolatedFromTownhallSeedAndFallbackNamespaces()
    {
        var catalog = new ActorCatalog();

        var customTownhallId = catalog.RegisterOrGetCustomPanelActor(
            "agent-1",
            "Custom Townhall Id",
            "avatar-custom-townhall");
        var customAlpha = catalog.RegisterOrGetCustomPanelActor(
            "alpha",
            "Custom Alpha",
            "avatar-custom-alpha");
        var customFallback = catalog.RegisterOrGetCustomPanelActor(
            "agent-1",
            "Custom Townhall Id",
            "avatar-custom-townhall");

        Assert.Equal("panel-custom:agent-1", customTownhallId.Id.Value);
        Assert.Equal("panel-custom:alpha", customAlpha.Id.Value);
        Assert.Same(customTownhallId, customFallback);

        Assert.NotEqual(catalog.CanonicalTownhallAgent.Id, customTownhallId.Id);
        Assert.NotEqual(catalog.GetPanelSeedActor(0).Id, customAlpha.Id);

        var fallback = catalog.GetOrRegisterPanelFallbackActor(1);
        Assert.Equal("panel-fallback:1", fallback.Id.Value);
        Assert.Equal("agent-1", fallback.ProjectedLegacyId);
        Assert.NotEqual(customTownhallId.Id, fallback.Id);
    }

    [Fact]
    public void RegisterOrGetCustomPanelActor_AcceptsEmptyLegacyValues()
    {
        var catalog = new ActorCatalog();

        var actor = catalog.RegisterOrGetCustomPanelActor(string.Empty, string.Empty, string.Empty);

        Assert.Equal("panel-custom:", actor.Id.Value);
        Assert.Equal(string.Empty, actor.ProjectedLegacyId);
        Assert.Equal(string.Empty, actor.DisplayName);
        Assert.Equal(string.Empty, actor.AvatarResourceKey);
    }

    [Fact]
    public async Task RegisterOrGetCustomPanelActor_ConcurrentConflictingFirstRegistration_ThrowsForLoser()
    {
        var catalog = new ActorCatalog();
        var barrier = new Barrier(2);
        var exceptions = new ConcurrentBag<Exception>();
        var results = new ConcurrentBag<string>();

        var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                var actor = catalog.RegisterOrGetCustomPanelActor(
                    "agent-x",
                    i == 0 ? "X Agent" : "Different",
                    "avatar_x");
                results.Add(actor.DisplayName);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Single(results);
        Assert.Single(exceptions);
        Assert.IsType<ArgumentException>(exceptions.Single());
        Assert.True(catalog.TryGet(ActorId.PanelCustom("agent-x"), out var actor));
        Assert.Equal(results.Single(), actor!.DisplayName);
    }

    [Fact]
    public void PanelFallbackActors_PreserveLegacyProjectedIdAndName()
    {
        var catalog = new ActorCatalog();

        var fallback = catalog.GetOrRegisterPanelFallbackActor(1);

        Assert.Equal("panel-fallback:1", fallback.Id.Value);
        Assert.Equal("agent-1", fallback.ProjectedLegacyId);
        Assert.Equal("Agent 1", fallback.DisplayName);
        Assert.Equal("Icon.Avatar", fallback.AvatarResourceKey);
    }
}
