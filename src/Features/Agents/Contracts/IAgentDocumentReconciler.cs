using System.Threading;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Editor-owned reconciliation port consumed by the action broker after a
/// confirmed workspace file mutation. Implementations must not depend on
/// Editor Presentation types.
/// </summary>
internal interface IAgentDocumentReconciler
{
    AgentDocumentReconciliationResult ReconcileAfterMutation(
        WorkspaceActionScope scope,
        IWorkspaceActionAuthority workspaceAuthority,
        AgentFileActionProposal proposal,
        AgentFileMutationResult mutationResult,
        CancellationToken cancellationToken);
}
