using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Presentation.Transparency;

internal sealed class AgentSessionContinuityAvailabilityState
{
    public static AgentSessionContinuityAvailabilityState Initial { get; } = new(
        recoverableCount: 0,
        terminalCount: 0,
        indeterminateCount: 0,
        interruptedSessions: Array.Empty<AgentSessionContinuityInterruptedSession>());

    public AgentSessionContinuityAvailabilityState(
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
