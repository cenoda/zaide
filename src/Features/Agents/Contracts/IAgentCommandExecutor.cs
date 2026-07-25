using System.Threading;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Zaide-owned bounded command execution adapter for one approved resolved
/// command. This is the action-control process boundary: it revalidates the
/// captured workspace root and working-directory containment, starts only the
/// bound canonical executable with an ordered argument vector, constructs the
/// locked environment, enforces output and time budgets, and owns complete
/// process-tree termination. It never invokes a shell and is never exposed to
/// a backend.
/// </summary>
internal interface IAgentCommandExecutor
{
    AgentCommandExecutionResult Execute(
        WorkspaceActionScope scope,
        AgentResolvedCommand resolvedCommand,
        CancellationToken cancellationToken);
}
