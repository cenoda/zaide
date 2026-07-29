using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Transparency;

internal sealed class AgentTransparencyExportSection
{
    public AgentTransparencyExportSection(
        AgentDurableRecordClass recordClass,
        int recordCount,
        bool isUnavailable,
        string? unavailableReason = null,
        IReadOnlyList<string>? payloadJsonLines = null)
    {
        RecordClass = recordClass;
        RecordCount = recordCount;
        IsUnavailable = isUnavailable;
        UnavailableReason = unavailableReason;
        PayloadJsonLines = payloadJsonLines ?? Array.Empty<string>();
    }

    public AgentDurableRecordClass RecordClass { get; }

    public int RecordCount { get; }

    public bool IsUnavailable { get; }

    public string? UnavailableReason { get; }

    public IReadOnlyList<string> PayloadJsonLines { get; }
}
