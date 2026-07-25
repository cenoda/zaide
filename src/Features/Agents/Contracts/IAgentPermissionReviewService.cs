using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Service that presents action review requests to the user on the Zaide-owned
/// visible review surface. Accepts the captured workspace scope so the
/// permission UI can display both the normalized workspace-relative path and
/// the resolved absolute path.
/// </summary>
internal interface IAgentPermissionReviewService
{
    ValueTask<AgentPermissionDecision> RequestDecisionAsync(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken);
}
