using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryExportPackage
{
    public AgentMemoryExportPackage(
        AgentDurableWorkspaceStorageKey workspaceKey,
        int schemaVersion,
        DateTimeOffset exportedAtUtc,
        IReadOnlyList<AgentMemoryRecord> records,
        bool partialUnavailable)
    {
        WorkspaceKey = workspaceKey;
        SchemaVersion = schemaVersion;
        ExportedAtUtc = exportedAtUtc;
        Records = records;
        PartialUnavailable = partialUnavailable;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public int SchemaVersion { get; }

    public DateTimeOffset ExportedAtUtc { get; }

    public IReadOnlyList<AgentMemoryRecord> Records { get; }

    public bool PartialUnavailable { get; }
}
