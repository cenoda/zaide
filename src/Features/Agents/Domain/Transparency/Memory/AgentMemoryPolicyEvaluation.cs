using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryPolicyEvaluation
{
    public AgentMemoryPolicyEvaluation(
        AgentMemoryConflictKind conflictKind,
        bool isPoisoningSuspect,
        bool isStaleFact,
        string? reason = null)
    {
        ConflictKind = conflictKind;
        IsPoisoningSuspect = isPoisoningSuspect;
        IsStaleFact = isStaleFact;
        Reason = reason;
    }

    public AgentMemoryConflictKind ConflictKind { get; }

    public bool IsPoisoningSuspect { get; }

    public bool IsStaleFact { get; }

    public string? Reason { get; }

    public static AgentMemoryPolicyEvaluation Clean() =>
        new(AgentMemoryConflictKind.None, isPoisoningSuspect: false, isStaleFact: false);
}
