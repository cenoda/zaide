using System;
using System.Collections.Generic;
using System.Threading;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Presentation-side projection that observes the trace capture pipeline
/// without touching the agent event pipeline. The projection publishes a
/// read-only <see cref="AgentTraceAvailabilityState"/> snapshot; backend
/// adapters and the M1 record store remain the authoritative owners of the
/// underlying trace data.
/// </summary>
internal sealed class AgentTraceAvailabilityProjection : IDisposable
{
    private readonly AgentTraceCoordinator _coordinator;
    private readonly Func<string?> _workspaceRootProvider;
    private readonly Timer _refreshTimer;
    private readonly object _stateGate = new();
    private AgentTraceAvailabilityState _state = AgentTraceAvailabilityState.Initial;
    private bool _disposed;

    public AgentTraceAvailabilityProjection(
        AgentTraceCoordinator coordinator,
        Func<string?>? workspaceRootProvider = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaceRootProvider = workspaceRootProvider ?? (() => null);
        _refreshTimer = new Timer(
            _ => Refresh(),
            state: null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(5));
    }

    public AgentTraceAvailabilityState CurrentState
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<AgentTraceAvailabilityState>? StateChanged;

    public void Refresh() => Refresh(force: false);

    public void Refresh(bool force)
    {
        if (_disposed)
        {
            return;
        }

        var workspaceRoot = _workspaceRootProvider();
        AgentTraceInspectionSummary summary;
        try
        {
            summary = _coordinator.GetSummary(workspaceRoot);
        }
        catch
        {
            // Inspection is best-effort. M1 storage failures must not surface
            // as presentation errors.
            return;
        }

        var backpressureObserved = _coordinator.BackpressureDroppedCount > 0;
        var next = new AgentTraceAvailabilityState(
            captureEnabled: _coordinator.IsCaptureEnabled(),
            totalRecords: summary.TotalRecords,
            totalPayloadBytes: summary.TotalPayloadBytes,
            lastCapturedAtUtc: summary.NewestCapturedAtUtc,
            countsByState: summary.CountsByState,
            backpressureObserved: backpressureObserved);

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
                // Subscriber failures must not affect the projection state.
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

    private static bool StateEquals(AgentTraceAvailabilityState left, AgentTraceAvailabilityState right)
    {
        if (left.CaptureEnabled != right.CaptureEnabled) return false;
        if (left.TotalRecords != right.TotalRecords) return false;
        if (left.TotalPayloadBytes != right.TotalPayloadBytes) return false;
        if (left.BackpressureObserved != right.BackpressureObserved) return false;
        if (left.LastCapturedAtUtc != right.LastCapturedAtUtc) return false;
        if (left.CountsByState.Count != right.CountsByState.Count) return false;
        foreach (var pair in left.CountsByState)
        {
            if (!right.CountsByState.TryGetValue(pair.Key, out var otherCount) || otherCount != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
