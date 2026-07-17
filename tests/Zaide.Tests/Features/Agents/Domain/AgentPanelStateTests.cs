using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

/// <summary>
/// Tests for <see cref="AgentPanelState"/> — the minimal single-panel state shape.
/// Phase 5.1.1: confirms the shape is narrow, contains no routing or provider
/// abstractions, and matches the documented host ownership decision.
/// </summary>
public class AgentPanelStateTests
{
    [Fact]
    public void DefaultState_HasExpectedDefaultValues()
    {
        var state = new AgentPanelState();

        Assert.Empty(state.PanelId);
        Assert.Empty(state.AgentId);
        Assert.Empty(state.AgentName);
        Assert.Empty(state.AvatarResourceKey);
        Assert.Equal("Idle", state.Status);
        Assert.False(state.IsBusy);
        Assert.Empty(state.OutputHistory);
        Assert.Empty(state.DraftInput);
    }

    [Fact]
    public void DefaultState_OutputHistory_IsNotNull()
    {
        var state = new AgentPanelState();
        Assert.NotNull(state.OutputHistory);
    }

    [Fact]
    public void CanSetAndReadPanelId()
    {
        var state = new AgentPanelState { PanelId = "panel-1" };
        Assert.Equal("panel-1", state.PanelId);
    }

    [Fact]
    public void CanSetAndReadAgentId()
    {
        var state = new AgentPanelState { AgentId = "agent-gpt" };
        Assert.Equal("agent-gpt", state.AgentId);
    }

    [Fact]
    public void CanSetAndReadAgentName()
    {
        var state = new AgentPanelState { AgentName = "GPT-4" };
        Assert.Equal("GPT-4", state.AgentName);
    }

    [Fact]
    public void CanSetAndReadAvatarResourceKey()
    {
        var state = new AgentPanelState { AvatarResourceKey = "AvatarGpt4" };
        Assert.Equal("AvatarGpt4", state.AvatarResourceKey);
    }

    [Fact]
    public void CanSetAndReadStatus()
    {
        var state = new AgentPanelState { Status = "Thinking" };
        Assert.Equal("Thinking", state.Status);
    }

    [Fact]
    public void CanSetAndReadDraftInput()
    {
        var state = new AgentPanelState { DraftInput = "Hello, agent." };
        Assert.Equal("Hello, agent.", state.DraftInput);
    }

    [Fact]
    public void OutputHistory_CanAddAndEnumerateEntries()
    {
        var state = new AgentPanelState();
        state.OutputHistory.Add("User: Hello");
        state.OutputHistory.Add("Agent: Hi there");

        Assert.Equal(2, state.OutputHistory.Count);
        Assert.Equal("User: Hello", state.OutputHistory[0]);
        Assert.Equal("Agent: Hi there", state.OutputHistory[1]);
    }

    [Fact]
    public void OutputHistory_StartsEmpty()
    {
        var state = new AgentPanelState();
        Assert.Empty(state.OutputHistory);
    }

    [Fact]
    public void Shape_ContainsNoRoutingMetadata()
    {
        // Phase 5.1.1 constraint: no routing fields like RouteTarget, @mention,
        // ChannelId, or DestinationAgentId should exist.
        var type = typeof(AgentPanelState);
        var props = type.GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("PanelId", props);
        Assert.Contains("AgentId", props);
        Assert.Contains("AgentName", props);
        Assert.Contains("AvatarResourceKey", props);
        Assert.Contains("Status", props);
        Assert.Contains("DraftInput", props);

        // Explicitly verify routing-related terms are absent
        Assert.DoesNotContain("RouteTarget", props);
        Assert.DoesNotContain("ChannelId", props);
        Assert.DoesNotContain("DestinationAgentId", props);
    }

    [Fact]
    public void Shape_ContainsNoProviderPlatformAbstractions()
    {
        // Phase 5.1.1 constraint: no provider-platform fields like EndpointUrl,
        // ApiKey, ModelName, ProviderName, or ConnectionConfig should exist.
        var type = typeof(AgentPanelState);
        var props = type.GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("EndpointUrl", props);
        Assert.DoesNotContain("ApiKey", props);
        Assert.DoesNotContain("ModelName", props);
        Assert.DoesNotContain("ProviderName", props);
        Assert.DoesNotContain("ConnectionConfig", props);
    }

    [Fact]
    public void Shape_HasExactlyExpectedFields()
    {
        // Phase 5.1.1 shape: PanelId, AgentId, AgentName, AvatarResourceKey,
        // Status, OutputHistory, DraftInput — no more, no less.
        // M2: AgentPanelState extends ReactiveObject. ReactiveObject exposes
        // inherited public properties (e.g. Changed, Changing). We filter to
        // only properties declared directly on AgentPanelState.
        // M3: Added IsBusy for input-surface disable during in-flight requests.
        var type = typeof(AgentPanelState);
        var props = type.GetProperties()
            .Where(p => p.DeclaringType == typeof(AgentPanelState))
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        var expected = new[] { "AgentId", "AgentName", "AvatarResourceKey", "DraftInput", "IsBusy", "OutputHistory", "PanelId", "Status" };
        Assert.Equal(expected, props);
    }

    // ── M2: Reactive property behavior ──────────────────────────────────────

    [Fact]
    public void Status_Setter_RaisesPropertyChanged()
    {
        var state = new AgentPanelState();
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
        var state = new AgentPanelState();
        var changedCount = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.Status))
                changedCount++;
        };

        // Set to non-default value
        state.Status = "Thinking";
        Assert.Equal(1, changedCount);

        // Set same value — should not raise
        state.Status = "Thinking";
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void DraftInput_Setter_RaisesPropertyChanged()
    {
        var state = new AgentPanelState();
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
        var state = new AgentPanelState();
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
    public void NonReactiveProperties_DoNotRaiseChangeForStatusOrDraftInput()
    {
        // Verify that setting non-reactive properties (e.g. AgentName) does
        // not incorrectly fire change notifications for reactive properties.
        var state = new AgentPanelState();
        var statusChanged = 0;
        var draftChanged = 0;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelState.Status))
                statusChanged++;
            if (e.PropertyName == nameof(AgentPanelState.DraftInput))
                draftChanged++;
        };

        state.AgentName = "New Agent";
        state.PanelId = "new-panel";

        Assert.Equal(0, statusChanged);
        Assert.Equal(0, draftChanged);
    }

    // ── M3: IsBusy reactive property behavior ───────────────────────────────

    [Fact]
    public void IsBusy_Default_IsFalse()
    {
        var state = new AgentPanelState();
        Assert.False(state.IsBusy);
    }

    [Fact]
    public void IsBusy_Setter_RaisesPropertyChanged()
    {
        var state = new AgentPanelState();
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
        var state = new AgentPanelState();
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
        var state = new AgentPanelState();
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
