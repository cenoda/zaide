using Zaide.Features.SourceControl.Application;

namespace Zaide.Features.SourceControl.Domain;

/// <summary>
/// Passive container for Source Control session data. Holds the latest truthful
/// repository snapshot produced by <see cref="IGitRepositoryService"/>; it is NOT a
/// source of truth and seeds no demo data. The git read seam owns the truth; this
/// only mirrors the most recent read for later phases and keeps the user-entered
/// commit draft.
/// </summary>
public class SourceControlState
{
    /// <summary>
    /// Latest repository snapshot read from the git seam by a live consumer. Null
    /// until a repository snapshot has been loaded. The
    /// <see cref="IGitRepositoryService"/> is the source of truth.
    /// </summary>
    public RepositoryStatusSnapshot? Snapshot { get; set; }

    /// <summary>
    /// User-entered commit message draft. Pure session input; carries no git link.
    /// </summary>
    public string CommitMessageDraft { get; set; } = string.Empty;
}
