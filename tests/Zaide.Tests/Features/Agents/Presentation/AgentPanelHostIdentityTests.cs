using System;
using Xunit;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Presentation;

public sealed class AgentPanelHostIdentityTests
{
    [Fact]
    public void CreatePanel_CustomConflict_ThrowsBeforeHostMutation()
    {
        var catalog = new ActorCatalog();
        var host = new AgentPanelHost(catalog);
        var first = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        var ex = Assert.Throws<ArgumentException>(() =>
            host.CreatePanel("agent-x", "Different Name", "avatar_x"));

        Assert.Contains("conflicting identity", ex.Message, StringComparison.Ordinal);
        Assert.Single(host.Panels);
        Assert.Same(first, host.ActivePanel);
        Assert.True(catalog.TryGet(ActorId.PanelCustom("agent-x"), out var actor));
        Assert.Equal("X Agent", actor!.DisplayName);
    }

    [Fact]
    public void CreatePanel_CustomIdenticalReuse_AllowsMultiplePanels()
    {
        var catalog = new ActorCatalog();
        var host = new AgentPanelHost(catalog);

        var first = host.CreatePanel("agent-x", "X Agent", "avatar_x");
        var second = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.Equal(2, host.Panels.Count);
        Assert.NotEqual(first.PanelId, second.PanelId);
        Assert.Equal(first.AgentId, second.AgentId);
        Assert.Equal(first.AgentName, second.AgentName);
        Assert.Equal(first.AvatarResourceKey, second.AvatarResourceKey);
    }
}
