using System.Threading;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Zaide-owned bounded mutation adapter for one accepted workspace file
/// proposal. This is the action-control security boundary for writes and
/// deletes: it revalidates the captured workspace root, enforces path
/// containment and symbolic-link defenses, applies optimistic concurrency using
/// the captured base revision, and uses same-directory temporary files with
/// atomic replacement where supported. It is never the editor file service and
/// is never exposed to a backend.
/// </summary>
internal interface IAgentFileMutator
{
    AgentFileMutationResult Apply(
        WorkspaceActionScope scope,
        AgentFileActionProposal proposal,
        AgentActionPayload payload,
        CancellationToken cancellationToken);
}
