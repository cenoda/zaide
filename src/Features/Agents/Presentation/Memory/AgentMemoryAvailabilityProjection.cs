using System;
using System.Threading;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Presentation.Memory;

internal sealed class AgentMemoryAvailabilityProjection : IDisposable
{
    private readonly AgentMemoryCoordinator _coordinator;
    private readonly Func<string?> _workspaceRootProvider;
    private readonly Timer _refreshTimer;
    private readonly object _stateGate = new();
    private AgentMemoryAvailabilityState _state = AgentMemoryAvailabilityState.Initial;
    private bool _disposed;

    public AgentMemoryAvailabilityProjection(
        AgentMemoryCoordinator coordinator,
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

    public AgentMemoryAvailabilityState CurrentState
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<AgentMemoryAvailabilityState>? StateChanged;

    public void Refresh() => Refresh(force: false);

    public void Refresh(bool force)
    {
        if (_disposed)
        {
            return;
        }

        AgentMemoryInspectionSummary summary;
        try
        {
            var workspaceKey = _coordinator.ResolveWorkspaceKey(_workspaceRootProvider());
            summary = _coordinator.Inspector.GetSummary(workspaceKey);
        }
        catch
        {
            return;
        }

        var next = new AgentMemoryAvailabilityState(
            summary.TotalRecords,
            summary.ActiveRecords,
            summary.DisabledRecords,
            summary.SupersededRecords,
            summary.DeletedRecords,
            summary.PoisoningSuspects,
            summary.StaleFacts,
            summary.NewestUpdatedAtUtc);

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
        _refreshTimer.Dispose();
    }

    private static bool StateEquals(
        AgentMemoryAvailabilityState left,
        AgentMemoryAvailabilityState right)
    {
        if (left.TotalRecords != right.TotalRecords) return false;
        if (left.ActiveRecords != right.ActiveRecords) return false;
        if (left.DisabledRecords != right.DisabledRecords) return false;
        if (left.SupersededRecords != right.SupersededRecords) return false;
        if (left.DeletedRecords != right.DeletedRecords) return false;
        if (left.PoisoningSuspects != right.PoisoningSuspects) return false;
        if (left.StaleFacts != right.StaleFacts) return false;
        if (left.NewestUpdatedAtUtc != right.NewestUpdatedAtUtc) return false;
        return true;
    }
}
