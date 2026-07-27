using System;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase19ContractsRunOutcomeTests
{
    [Fact]
    public void Phase19Contracts_RunOutcome_CompletedRequiresFinalAssistantText()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NativeHarnessRunOutcome(NativeHarnessRunTerminationKind.Completed));

        Assert.Equal("finalAssistantText", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_RunOutcome_FailedRequiresFailureReason()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NativeHarnessRunOutcome(NativeHarnessRunTerminationKind.Failed));

        Assert.Equal("failureReason", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_RunOutcome_CancelledDoesNotRequireAssistantText()
    {
        var outcome = new NativeHarnessRunOutcome(NativeHarnessRunTerminationKind.Cancelled);

        Assert.Equal(NativeHarnessRunTerminationKind.Cancelled, outcome.TerminationKind);
        Assert.Null(outcome.FinalAssistantText);
    }

    [Fact]
    public void Phase19Contracts_RunOutcome_IndeterminateAllowsLateCompletionDisposition()
    {
        var outcome = new NativeHarnessRunOutcome(
            NativeHarnessRunTerminationKind.Indeterminate,
            lateCompletionDisposition:
                NativeHarnessLateCompletionDisposition.ObservedAndReportedIndeterminate);

        Assert.Equal(
            NativeHarnessLateCompletionDisposition.ObservedAndReportedIndeterminate,
            outcome.LateCompletionDisposition);
    }

    [Fact]
    public void Phase19Contracts_CancellationState_TracksLateCompletionAfterRequest()
    {
        var initial = NativeHarnessCancellationState.Initial();
        var requested = initial.WithCancellationRequested();
        var late = requested.WithLateCompletionObserved(
            NativeHarnessLateCompletionDisposition.ObservedAndDiscarded);

        Assert.False(initial.CancellationRequested);
        Assert.True(requested.CancellationRequested);
        Assert.True(late.HasLateCompletion);
    }

    [Fact]
    public void Phase19Contracts_TurnBudget_DefaultMatchesProviderProtocol()
    {
        var budget = NativeHarnessTurnBudget.CreateDefault();

        Assert.Equal(NativeHarnessProviderProtocol.DefaultMaxTurns, budget.MaxTurns);
        Assert.Equal(NativeHarnessProviderProtocol.DefaultMaxTurns, budget.RemainingTurns);
    }

    [Fact]
    public void Phase19Contracts_TurnBudget_ConsumeTurn_IsImmutable()
    {
        var initial = NativeHarnessTurnBudget.Create(maxTurns: 2);
        var afterOne = initial.ConsumeTurn();

        Assert.Equal(2, initial.RemainingTurns);
        Assert.Equal(1, afterOne.RemainingTurns);
        Assert.Throws<InvalidOperationException>(() => afterOne.ConsumeTurn().ConsumeTurn().ConsumeTurn());
    }
}
