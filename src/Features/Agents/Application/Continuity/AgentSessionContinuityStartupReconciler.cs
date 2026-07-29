using System;
using System.Threading;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AgentSessionContinuityStartupReconciler
{
    private readonly IAgentSessionContinuityCoordinator _coordinator;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly Func<string?> _workspaceRootProvider;
    private int _startupReconciled;

    public AgentSessionContinuityStartupReconciler(
        IAgentSessionContinuityCoordinator coordinator,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        Func<string?>? workspaceRootProvider = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _workspaceRootProvider = workspaceRootProvider ?? (() => Environment.CurrentDirectory);
    }

    public AgentSessionContinuityReconcileSummary ReconcileOnStartupIfNeeded()
    {
        if (Interlocked.CompareExchange(ref _startupReconciled, 1, 0) != 0)
        {
            return new AgentSessionContinuityReconcileSummary(
                0,
                0,
                0,
                Array.Empty<AgentSessionContinuityInterruptedSession>());
        }

        var workspaceRoot = _workspaceRootProvider();
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return new AgentSessionContinuityReconcileSummary(
                0,
                0,
                0,
                Array.Empty<AgentSessionContinuityInterruptedSession>());
        }

        var workspaceKey = _workspaceKeyResolver.Resolve(workspaceRoot);
        return _coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            workspaceKey,
            workspaceRoot,
            isStartup: true));
    }
}
