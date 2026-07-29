using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityReconcileSummary
{
    public AgentSessionContinuityReconcileSummary(
        int recoverableCount,
        int terminalCount,
        int indeterminateCount,
        IReadOnlyList<AgentSessionContinuityInterruptedSession> interruptedSessions)
    {
        RecoverableCount = recoverableCount;
        TerminalCount = terminalCount;
        IndeterminateCount = indeterminateCount;
        InterruptedSessions = interruptedSessions;
    }

    public int RecoverableCount { get; }

    public int TerminalCount { get; }

    public int IndeterminateCount { get; }

    public IReadOnlyList<AgentSessionContinuityInterruptedSession> InterruptedSessions { get; }
}
