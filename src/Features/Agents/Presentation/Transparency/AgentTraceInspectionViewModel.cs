using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;

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

    public AgentTraceInspectionViewModel(
        AgentTraceCoordinator coordinator,
        AgentTraceAvailabilityProjection availability)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
    }

    public AgentTraceAvailabilityState Availability => _availability.CurrentState;

    public string AvailabilityCaption => Availability.FormatStatusCaption();

    public bool BackpressureObserved => Availability.BackpressureObserved;

    public Task<AgentTraceInspectionSummary> LoadSummaryAsync() =>
        Task.FromResult(_coordinator.GetSummary(workspaceRoot: null));

    public Task<IReadOnlyList<AgentTraceRecord>> LoadRecordsAsync(
        long afterOrderingSequence,
        int maxRecords) =>
        Task.FromResult(_coordinator.GetRecords(
            workspaceRoot: null,
            afterOrderingSequence: afterOrderingSequence,
            maxRecords: maxRecords));
}
