using System.Reflection;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ActorIdTests
{
    [Fact]
    public void Default_HasEmptyValueAndStableHashCode()
    {
        var id = default(ActorId);

        Assert.Equal(string.Empty, id.Value);
        Assert.Equal(0, id.GetHashCode());
    }

    [Fact]
    public void FactoryMethods_ProduceLockedNamespaces()
    {
        Assert.Equal("human:user-1", ActorId.HumanUser.Value);
        Assert.Equal("panel-seed:alpha", ActorId.PanelSeed("alpha").Value);
        Assert.Equal("panel-fallback:1", ActorId.PanelFallback(1).Value);
        Assert.Equal("panel-custom:agent-x", ActorId.PanelCustom("agent-x").Value);
        Assert.Equal("panel-custom:", ActorId.PanelCustom(string.Empty).Value);
    }

    [Fact]
    public void PublicSurface_HasNoUserConstructibleConstructor()
    {
        var constructors = typeof(ActorId).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(constructors);
    }
}
