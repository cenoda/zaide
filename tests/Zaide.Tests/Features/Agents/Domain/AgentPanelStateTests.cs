using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

/// <summary>
/// Tests for <see cref="AgentPanelState"/> — the minimal single-panel state shape.
/// Phase 5.1.1: confirms the shape is narrow, contains no routing or provider
/// abstractions, and matches the documented host ownership decision.
/// </summary>
public class AgentPanelStateTests
{
    private static AgentPanelState CreateState(
        string legacyId = "agent-test",
        string displayName = "Test Agent",
        string avatar = "avatar-test",
        ConversationId? conversationId = null) =>
        AgentPanelTestSupport.CreatePanelState(legacyId, displayName, avatar);

    [Fact]
    public void Constructor_BindsIdentityProjectionsToCanonicalActor()
    {
        var state = CreateState("agent-gpt", "GPT-4", "AvatarGpt4");

        Assert.Equal("panel-custom:agent-gpt", state.ActorId.Value);
        Assert.Equal("agent-gpt", state.AgentId);
        Assert.Equal("GPT-4", state.AgentName);
        Assert.Equal("AvatarGpt4", state.AvatarResourceKey);
        Assert.Equal("Idle", state.Status);
        Assert.False(state.IsBusy);
        Assert.Empty(state.OutputHistory);
        Assert.Empty(state.DraftInput);
    }

    [Fact]
    public void DefaultState_OutputHistory_IsNotNull()
    {
        var state = CreateState();
        Assert.NotNull(state.OutputHistory);
    }

    [Fact]
    public void CanSetAndReadPanelId()
    {
        var state = CreateState();
        state.PanelId = "panel-1";
        Assert.Equal("panel-1", state.PanelId);
    }

    [Fact]
    public void IdentityProjections_AreReadOnly()
    {
        var type = typeof(AgentPanelState);

        Assert.Null(type.GetProperty(nameof(AgentPanelState.AgentId))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(AgentPanelState.AgentName))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(AgentPanelState.AvatarResourceKey))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(AgentPanelState.ActorId))!.SetMethod);
    }

    [Fact]
    public void CanSetAndReadStatus()
    {
        var state = CreateState();
        state.Status = "Thinking";
        Assert.Equal("Thinking", state.Status);
    }

    [Fact]
    public void CanSetAndReadDraftInput()
    {
        var state = CreateState();
        state.DraftInput = "Hello, agent.";
        Assert.Equal("Hello, agent.", state.DraftInput);
    }

    [Fact]
    public void OutputHistory_ProjectsAuthoritativeTypedEntries()
    {
        var store = Conversations.ConversationsTestSupport.CreateStore();
        var state = AgentPanelTestSupport.CreatePanelState(store: store);

        AgentPanelTestSupport.AppendUserChat(store, state, "Hello");
        AgentPanelTestSupport.AppendAssistantResponse(store, state, "Hi there");

        Assert.Equal(2, state.OutputHistory.Count);
        Assert.Equal("User: Hello", state.OutputHistory[0]);
        Assert.Equal("Assistant: Hi there", state.OutputHistory[1]);
    }

    [Fact]
    public void OutputHistory_IsNotIndependentlyMutable()
    {
        var state = CreateState();

        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.IList)state.OutputHistory).Add("User: forged"));
    }

    [Fact]
    public void OutputHistory_StartsEmpty()
    {
        var state = CreateState();
        Assert.Empty(state.OutputHistory);
    }

    [Fact]
    public void Shape_ContainsNoRoutingMetadata()
    {
        var type = typeof(AgentPanelState);
        var props = type.GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("PanelId", props);
        Assert.Contains("ActorId", props);
        Assert.Contains("AgentId", props);
        Assert.Contains("AgentName", props);
        Assert.Contains("AvatarResourceKey", props);
        Assert.Contains("Status", props);
        Assert.Contains("DraftInput", props);

        Assert.DoesNotContain("RouteTarget", props);
        Assert.DoesNotContain("ChannelId", props);
        Assert.DoesNotContain("DestinationAgentId", props);
    }

    [Fact]
    public void Shape_ContainsNoProviderPlatformAbstractions()
    {
        var type = typeof(AgentPanelState);
        var props = type.GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("EndpointUrl", props);
        Assert.DoesNotContain("ApiKey", props);
        Assert.DoesNotContain("ModelName", props);
        Assert.DoesNotContain("ProviderName", props);
        Assert.DoesNotContain("ConnectionConfig", props);
    }

    [Fact]
    public void Constructor_RejectsDefaultConversationId()
    {
        var actor = new Actor(
            ActorId.PanelCustom("agent-test"),
            ActorKind.Agent,
            "agent-test",
            "Test Agent",
            "avatar-test");
        var store = Conversations.ConversationsTestSupport.CreateStore();
        var conversation = store.CreateDirectConversation(ActorId.HumanUser, actor.Id);
        var projection = new Zaide.Features.Agents.Application.AgentPanelOutputHistoryProjection(
            store,
            conversation.Id);

        Assert.Throws<ArgumentException>(() => new AgentPanelState(actor, default, projection.Lines));
    }

    [Fact]
    public void ConversationId_IsImmutableAfterConstruction()
    {
        var conversationId = ConversationId.NewDirect();
        var store = Conversations.ConversationsTestSupport.CreateStore();
        var actor = new Actor(
            ActorId.PanelCustom("agent-test"),
            ActorKind.Agent,
            "agent-test",
            "Test Agent",
            "avatar-test");
        store.CreateDirectConversation(ActorId.HumanUser, actor.Id);
        var state = AgentPanelTestSupport.CreatePanelState(store: store);

        Assert.NotEqual(default, state.ConversationId);

        var property = typeof(AgentPanelState).GetProperty(nameof(AgentPanelState.ConversationId));
        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    [Fact]
    public void Shape_HasExactlyExpectedFields()
    {
        var type = typeof(AgentPanelState);
        var props = type.GetProperties()
            .Where(p => p.DeclaringType == typeof(AgentPanelState))
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        var expected = new[]
        {
            "ActorId",
            "AgentId",
            "AgentName",
            "AvatarResourceKey",
            "ConversationId",
            "DraftInput",
            "IsBusy",
            "OutputHistory",
            "PanelId",
            "Status"
        };
        Assert.Equal(expected, props);
    }

    [Fact]
    public void Status_Setter_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.Status))
                changedCount++;
        };

        state.Status = "Thinking";
        Assert.Equal(1, changedCount);
        Assert.Equal("Thinking", state.Status);
    }

    [Fact]
    public void Status_SameValue_DoesNotRaiseChange()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.Status))
                changedCount++;
        };

        state.Status = "Thinking";
        Assert.Equal(1, changedCount);

        state.Status = "Thinking";
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void DraftInput_Setter_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.DraftInput))
                changedCount++;
        };

        state.DraftInput = "Hello";
        Assert.Equal(1, changedCount);
        Assert.Equal("Hello", state.DraftInput);
    }

    [Fact]
    public void DraftInput_SameValue_DoesNotRaiseChange()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.DraftInput))
                changedCount++;
        };

        state.DraftInput = "Hello";
        Assert.Equal(1, changedCount);

        state.DraftInput = "Hello";
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void PanelId_Change_DoesNotRaiseStatusOrDraftInputNotifications()
    {
        var state = CreateState();
        var statusChanged = 0;
        var draftChanged = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.Status))
                statusChanged++;
            if (e.PropertyName == nameof(AgentPanelState.DraftInput))
                draftChanged++;
        };

        state.PanelId = "new-panel";

        Assert.Equal(0, statusChanged);
        Assert.Equal(0, draftChanged);
    }

    [Fact]
    public void IsBusy_Default_IsFalse()
    {
        var state = CreateState();
        Assert.False(state.IsBusy);
    }

    [Fact]
    public void IsBusy_Setter_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.IsBusy))
                changedCount++;
        };

        state.IsBusy = true;
        Assert.Equal(1, changedCount);
        Assert.True(state.IsBusy);
    }

    [Fact]
    public void IsBusy_SameValue_DoesNotRaiseChange()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.IsBusy))
                changedCount++;
        };

        state.IsBusy = true;
        Assert.Equal(1, changedCount);

        state.IsBusy = true;
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void IsBusy_CanToggleBackToFalse()
    {
        var state = CreateState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.IsBusy))
                changedCount++;
        };

        state.IsBusy = true;
        Assert.True(state.IsBusy);
        Assert.Equal(1, changedCount);

        state.IsBusy = false;
        Assert.False(state.IsBusy);
        Assert.Equal(2, changedCount);
    }
}
