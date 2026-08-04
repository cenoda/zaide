using System;
using System.Threading;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Presentation.Transparency;

internal sealed class AgentUsageAvailabilityProjection : IDisposable
{
    private readonly AgentUsageCoordinator _coordinator;
    private readonly Func<string?> _workspaceRootProvider;
    private readonly Timer _refreshTimer;
    private readonly object _stateGate = new();
    private AgentUsageAvailabilityState _state = AgentUsageAvailabilityState.Initial;
    private bool _disposed;

    public AgentUsageAvailabilityProjection(
        AgentUsageCoordinator coordinator,
        IWorkspaceActionAuthority? workspaceAuthority = null)
        : this(
            coordinator,
            AgentContinuityWorkspaceRootProvider.CreateOpenedWorkspaceProvider(workspaceAuthority))
    {
    }

    public AgentUsageAvailabilityProjection(
        AgentUsageCoordinator coordinator,
        Func<string?>? workspaceRootProvider)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaceRootProvider = workspaceRootProvider ?? (() => null);
        _refreshTimer = new Timer(
            _ => Refresh(),
            state: null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(5));
    }

    public AgentUsageAvailabilityState CurrentState
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<AgentUsageAvailabilityState>? StateChanged;

    public void Refresh() => Refresh(force: false);

    public void Refresh(bool force)
    {
        if (_disposed)
        {
            return;
        }

        var workspaceRoot = _workspaceRootProvider();
        AgentUsageInspectionSummary summary;
        try
        {
            summary = _coordinator.GetSummary(workspaceRoot);
        }
        catch
        {
            return;
        }

        var next = new AgentUsageAvailabilityState(
            captureEnabled: _coordinator.IsCaptureEnabled(),
            totalRecords: summary.TotalRecords,
            totalCostValue: summary.TotalCostValue,
            totalCostCurrency: summary.TotalCostCurrency,
            lastCapturedAtUtc: summary.NewestCapturedAtUtc,
            countsByOrigin: summary.CountsByOrigin,
            hasVerifiedTotalCost: summary.HasVerifiedTotalCost);

        var changed = force;
        lock (_stateGate)
        {
            if (!StateEquals(_state, next))
            {
                _state = next;
                changed = true;
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
        using var _ = _refreshTimer;
        _refreshTimer.Dispose();
    }

    private static bool StateEquals(
        AgentUsageAvailabilityState left,
        AgentUsageAvailabilityState right)
    {
        if (left.CaptureEnabled != right.CaptureEnabled) return false;
        if (left.TotalRecords != right.TotalRecords) return false;
        if (left.TotalCostValue != right.TotalCostValue) return false;
        if (left.TotalCostCurrency != right.TotalCostCurrency) return false;
        if (left.HasVerifiedTotalCost != right.HasVerifiedTotalCost) return false;
        if (left.LastCapturedAtUtc != right.LastCapturedAtUtc) return false;
        if (left.CountsByOrigin.Count != right.CountsByOrigin.Count) return false;
        foreach (var pair in left.CountsByOrigin)
        {
            if (!right.CountsByOrigin.TryGetValue(pair.Key, out var otherCount)
                || otherCount != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
