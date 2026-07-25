using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase17ActionContractsLifecycleTests
{
    [Fact]
    public void AgentActionStatusTransitions_AllowClassifiedToAwaitingPermissionDecision()
    {
        Assert.True(AgentActionStatusTransitions.CanTransition(
            AgentActionStatus.Classified,
            AgentActionStatus.AwaitingPermissionDecision));
    }

    [Fact]
    public void AgentActionStatusTransitions_RejectTerminalToNonTerminal()
    {
        Assert.False(AgentActionStatusTransitions.CanTransition(
            AgentActionStatus.Succeeded,
            AgentActionStatus.Executing));
    }

    [Fact]
    public void AgentActionLifecycleState_EnforcesSingleTerminalOutcome()
    {
        var lifecycle = new AgentActionLifecycleState();
        lifecycle.TransitionTo(AgentActionStatus.Classified);
        lifecycle.TransitionTo(AgentActionStatus.AwaitingPermissionDecision);
        lifecycle.TransitionTo(AgentActionStatus.PermissionDenied);
        lifecycle.TransitionTo(AgentActionStatus.Denied);

        Assert.True(lifecycle.IsTerminal);
        Assert.Throws<InvalidOperationException>(() =>
            lifecycle.TransitionTo(AgentActionStatus.Executing));
    }

    [Fact]
    public void AgentPermissionDecisionStatusTransitions_AllowPublishedToExpired()
    {
        Assert.True(AgentPermissionDecisionStatusTransitions.CanTransition(
            AgentPermissionDecisionStatus.Published,
            AgentPermissionDecisionStatus.Expired));
    }

    [Fact]
    public void AgentActionRunSlotTracker_RejectsSecondConcurrentReservation()
    {
        var tracker = new AgentActionRunSlotTracker();
        var first = AgentActionId.New();
        var second = AgentActionId.New();

        Assert.True(tracker.TryReserve(first));
        Assert.False(tracker.TryReserve(second));
    }
}
