using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Lightweight inspection entry point surfaced to the existing Agents and
/// Townhall presentation. The view model never owns trace data; it
/// delegates to <see cref="IAgentTraceInspector"/> and the
/// <see cref="AgentTraceAvailabilityProjection"/> so the durable record store
/// remains the single source of truth.
/// </summary>
internal sealed class AgentTraceInspectionViewModel
{
    private readonly AgentTraceCoordinator _coordinator;
    private readonly AgentTraceAvailabilityProjection _availability;
    private readonly System.Func<string?> _workspaceRootProvider;
    private readonly object _gate = new();
    private long? _selectedOrderingSequence;
    private IReadOnlyList<AgentTraceRecord> _records = Array.Empty<AgentTraceRecord>();
    private AgentTraceInspectionSummary? _summary;

    public AgentTraceInspectionViewModel(
        AgentTraceCoordinator coordinator,
        AgentTraceAvailabilityProjection availability,
        IWorkspaceActionAuthority? workspaceAuthority = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        _workspaceRootProvider = AgentContinuityWorkspaceRootProvider
            .CreateOpenedWorkspaceProvider(workspaceAuthority);
    }

    public AgentTraceAvailabilityState Availability => _availability.CurrentState;

    public string AvailabilityCaption => Availability.FormatStatusCaption();

    public bool BackpressureObserved => Availability.BackpressureObserved;

    public AgentTraceInspectionSummary? Summary
    {
        get
        {
            lock (_gate)
            {
                return _summary;
            }
        }
    }

    public IReadOnlyList<AgentTraceRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records;
            }
        }
    }

    public AgentTraceRecord? SelectedRecord
    {
        get
        {
            lock (_gate)
            {
                if (_selectedOrderingSequence is not { } sequence)
                {
                    return null;
                }

                foreach (var record in _records)
                {
                    if (record.OrderingSequence == sequence)
                    {
                        return record;
                    }
                }

                return null;
            }
        }
    }

    public Task<AgentTraceInspectionSummary> LoadSummaryAsync() =>
        Task.FromResult(_coordinator.GetSummary(_workspaceRootProvider()));

    public Task<IReadOnlyList<AgentTraceRecord>> LoadRecordsAsync(
        long afterOrderingSequence,
        int maxRecords) =>
        Task.FromResult(_coordinator.GetRecords(
            _workspaceRootProvider(),
            afterOrderingSequence: afterOrderingSequence,
            maxRecords: maxRecords));

    /// <summary>
    /// Reloads the opened-workspace page into presentation state and clears a
    /// stale selection when the sequence is no longer present.
    /// </summary>
    public void ReloadPresentation(int pageSize)
    {
        var summary = _coordinator.GetSummary(_workspaceRootProvider());
        var records = _coordinator.GetRecords(
            _workspaceRootProvider(),
            afterOrderingSequence: 0,
            maxRecords: pageSize);
        lock (_gate)
        {
            _summary = summary;
            _records = records;
            if (_selectedOrderingSequence is { } selected
                && records.All(record => record.OrderingSequence != selected))
            {
                _selectedOrderingSequence = null;
            }
        }

        _availability.Refresh(force: true);
    }

    public void SelectRecord(long? orderingSequence)
    {
        lock (_gate)
        {
            _selectedOrderingSequence = orderingSequence;
        }
    }

    public void EnableCapture()
    {
        _coordinator.EnableCapture();
        _availability.Refresh(force: true);
    }

    public void DisableCapture()
    {
        _coordinator.DisableCapture();
        _availability.Refresh(force: true);
    }

    public void Refresh() => _availability.Refresh(force: true);
}
