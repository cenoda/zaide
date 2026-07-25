namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Result of reconciling a confirmed disk mutation with open editor documents.
/// </summary>
internal enum AgentDocumentReconciliationOutcome
{
    /// <summary>No open document required reconciliation.</summary>
    NotApplicable = 0,

    /// <summary>A clean open document was reloaded from confirmed disk content.</summary>
    ReloadedClean,

    /// <summary>A dirty open document was preserved; disk and buffer diverge.</summary>
    ExternalConflict,

    /// <summary>A clean open document remains after confirmed disk deletion.</summary>
    DiskDeletedClean,

    /// <summary>A dirty open document was preserved and disk absence was flagged.</summary>
    DiskDeletedDirty,

    /// <summary>Workspace generation changed before reconciliation could apply.</summary>
    StaleWorkspace,

    /// <summary>Disk content changed again between mutation and reconciliation.</summary>
    PostMutationRace,
}
