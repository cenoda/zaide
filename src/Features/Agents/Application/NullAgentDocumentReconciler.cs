using System.Threading;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// No-op reconciler used when document reconciliation is not wired.
/// </summary>
internal sealed class NullAgentDocumentReconciler : IAgentDocumentReconciler
{
    public static NullAgentDocumentReconciler Instance { get; } = new();

    private NullAgentDocumentReconciler()
    {
    }

    public AgentDocumentReconciliationResult ReconcileAfterMutation(
        WorkspaceActionScope scope,
        IWorkspaceActionAuthority workspaceAuthority,
        AgentFileActionProposal proposal,
        AgentFileMutationResult mutationResult,
        CancellationToken cancellationToken) =>
        AgentDocumentReconciliationResult.Create(
            AgentDocumentReconciliationOutcome.NotApplicable,
            "Document reconciliation is not configured.");
}
