using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable turn budget for one Native Harness run. One model round consumes one turn.
/// Broker tool execution does not consume turns.
/// </summary>
internal sealed class NativeHarnessTurnBudget
{
    private NativeHarnessTurnBudget(int maxTurns, int consumedTurns)
    {
        if (maxTurns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTurns), maxTurns, "Max turns must be positive.");
        }

        if (consumedTurns < 0 || consumedTurns > maxTurns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consumedTurns),
                consumedTurns,
                "Consumed turns must be between zero and max turns.");
        }

        MaxTurns = maxTurns;
        ConsumedTurns = consumedTurns;
    }

    public int MaxTurns { get; }

    public int ConsumedTurns { get; }

    public int RemainingTurns => MaxTurns - ConsumedTurns;

    public bool IsExhausted => ConsumedTurns >= MaxTurns;

    public static NativeHarnessTurnBudget CreateDefault() =>
        new(NativeHarnessProviderProtocol.DefaultMaxTurns, consumedTurns: 0);

    public static NativeHarnessTurnBudget Create(int maxTurns) =>
        new(maxTurns, consumedTurns: 0);

    public NativeHarnessTurnBudget ConsumeTurn()
    {
        if (IsExhausted)
        {
            throw new InvalidOperationException("Turn budget is exhausted.");
        }

        return new NativeHarnessTurnBudget(MaxTurns, ConsumedTurns + 1);
    }
}
