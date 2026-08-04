using System;
using System.Threading;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Presentation.Transparency;

internal sealed class AgentSessionContinuityAvailabilityProjection : IDisposable
{
    private readonly IAgentSessionContinuityCoordinator _coordinator;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly Func<string?> _workspaceRootProvider;
    private readonly Timer _refreshTimer;
    private readonly object _stateGate = new();
    private AgentSessionContinuityAvailabilityState _state = AgentSessionContinuityAvailabilityState.Initial;
    private bool _disposed;

    public AgentSessionContinuityAvailabilityProjection(
        IAgentSessionContinuityCoordinator coordinator,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        IWorkspaceActionAuthority workspaceAuthority,
        Func<string?>? workspaceRootProvider = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _ = workspaceAuthority ?? throw new ArgumentNullException(nameof(workspaceAuthority));
        _workspaceRootProvider = workspaceRootProvider
            ?? AgentContinuityWorkspaceRootProvider.CreateOpenedWorkspaceProvider(workspaceAuthority);
        _refreshTimer = new Timer(_ => Refresh(), state: null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public AgentSessionContinuityAvailabilityState CurrentState
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<AgentSessionContinuityAvailabilityState>? StateChanged;

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var workspaceRoot = _workspaceRootProvider();
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return;
        }

        var workspaceKey = _workspaceKeyResolver.Resolve(workspaceRoot);
        AgentSessionContinuityReconcileSummary summary;
        try
        {
            summary = _coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
                workspaceKey,
                workspaceRoot,
                isStartup: false,
                origin: AgentSessionContinuityReconcileOrigin.WorkspaceOpen));
        }
        catch
        {
            return;
        }

        var next = new AgentSessionContinuityAvailabilityState(
            summary.RecoverableCount,
            summary.TerminalCount,
            summary.IndeterminateCount,
            summary.InterruptedSessions);

        bool changed;
        lock (_stateGate)
        {
            changed = next.RecoverableCount != _state.RecoverableCount
                || next.TerminalCount != _state.TerminalCount
                || next.IndeterminateCount != _state.IndeterminateCount
                || next.InterruptedSessions.Count != _state.InterruptedSessions.Count;
            if (changed)
            {
                _state = next;
            }
        }

        if (changed)
        {
            try
            {
                StateChanged?.Invoke(this, next);
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshTimer.Dispose();
    }
}
