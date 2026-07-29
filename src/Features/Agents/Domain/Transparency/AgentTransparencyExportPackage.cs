using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Transparency;

internal sealed class AgentTransparencyExportPackage
{
    public AgentTransparencyExportPackage(
        AgentDurableWorkspaceStorageKey workspaceKey,
        DateTimeOffset exportedAtUtc,
        IReadOnlyList<AgentTransparencyExportSection> sections,
        AgentTransparencyLifecycleStatus status)
    {
        WorkspaceKey = workspaceKey;
        ExportedAtUtc = exportedAtUtc;
        Sections = sections ?? throw new ArgumentNullException(nameof(sections));
        Status = status;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public DateTimeOffset ExportedAtUtc { get; }

    public IReadOnlyList<AgentTransparencyExportSection> Sections { get; }

    public AgentTransparencyLifecycleStatus Status { get; }
}
