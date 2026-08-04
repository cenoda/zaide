using System;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application.Continuity;

/// <summary>
/// Classifies workspace-owned interrupted sessions when a disposable/product
/// workspace opens. Distinct from application-start legacy CWD reconciliation.
/// </summary>
internal sealed class AgentSessionContinuityWorkspaceOpenReconciler : IDisposable
{
    private readonly IAgentSessionContinuityCoordinator _coordinator;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly AgentSessionContinuityConversationProjector _conversationProjector;
    private readonly IWorkspaceActionAuthority _workspaceAuthority;
    private readonly object _sync = new();
    private WorkspaceIdentity _lastIdentity;
    private WorkspaceGeneration _lastGeneration;
    private bool _hasReconciledOpen;

    public AgentSessionContinuityWorkspaceOpenReconciler(
        IAgentSessionContinuityCoordinator coordinator,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        AgentSessionContinuityConversationProjector conversationProjector,
        IWorkspaceActionAuthority workspaceAuthority)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _conversationProjector = conversationProjector
            ?? throw new ArgumentNullException(nameof(conversationProjector));
        _workspaceAuthority = workspaceAuthority ?? throw new ArgumentNullException(nameof(workspaceAuthority));
        _workspaceAuthority.ScopeInvalidated += OnWorkspaceScopeChanged;
    }

    public AgentSessionContinuityReconcileSummary ReconcileOnWorkspaceOpenIfNeeded()
    {
        if (!_workspaceAuthority.TryCaptureCurrentScope(out var scope)
            || string.IsNullOrWhiteSpace(scope.RootPath))
        {
            lock (_sync)
            {
                _hasReconciledOpen = false;
            }

            return EmptySummary();
        }

        lock (_sync)
        {
            if (_hasReconciledOpen
                && _lastIdentity == scope.Identity
                && _lastGeneration == scope.Generation)
            {
                return EmptySummary();
            }
        }

        var workspaceKey = _workspaceKeyResolver.Resolve(scope.RootPath);
        var summary = _coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            workspaceKey,
            scope.RootPath,
            isStartup: false,
            origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));

        _conversationProjector.ProjectReconcileSummary(
            summary,
            AgentSessionContinuityReconcileOrigin.WorkspaceOpen);

        lock (_sync)
        {
            _lastIdentity = scope.Identity;
            _lastGeneration = scope.Generation;
            _hasReconciledOpen = true;
        }

        return summary;
    }

    public void Dispose()
    {
        _workspaceAuthority.ScopeInvalidated -= OnWorkspaceScopeChanged;
    }

    private void OnWorkspaceScopeChanged()
    {
        try
        {
            ReconcileOnWorkspaceOpenIfNeeded();
        }
        catch
        {
        }
    }

    private static AgentSessionContinuityReconcileSummary EmptySummary() =>
        new(0, 0, 0, Array.Empty<AgentSessionContinuityInterruptedSession>());
}
