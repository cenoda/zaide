using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryRetrievalResult
{
    public AgentMemoryRetrievalResult(
        IReadOnlyList<AgentMemoryRecord> eligibleRecords,
        bool isUnavailable,
        string? unavailableReason = null)
    {
        EligibleRecords = eligibleRecords ?? throw new ArgumentNullException(nameof(eligibleRecords));
        IsUnavailable = isUnavailable;
        UnavailableReason = unavailableReason;
    }

    public IReadOnlyList<AgentMemoryRecord> EligibleRecords { get; }

    public bool IsUnavailable { get; }

    public string? UnavailableReason { get; }

    public static AgentMemoryRetrievalResult Unavailable(string reason) =>
        new(Array.Empty<AgentMemoryRecord>(), isUnavailable: true, unavailableReason: reason);

    public static AgentMemoryRetrievalResult Empty { get; } =
        new(Array.Empty<AgentMemoryRecord>(), isUnavailable: false);
}
