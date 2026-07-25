using System.Threading;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Zaide-owned bounded read adapter for one regular workspace file. This is the
/// action-control security boundary for reads: it canonicalizes the captured
/// workspace root, enforces path containment and symbolic-link/TOCTOU defenses,
/// rejects directories, special files, binary content, and oversized files, and
/// returns an attributable snapshot with a stable digest. It is never the editor
/// file service and is never exposed to a backend.
/// </summary>
internal interface IAgentFileReader
{
    AgentFileReadResult Read(
        WorkspaceActionScope scope,
        AgentWorkspaceRelativePath path,
        CancellationToken cancellationToken);
}
