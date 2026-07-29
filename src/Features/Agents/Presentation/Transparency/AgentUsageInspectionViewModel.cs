using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Presentation.Transparency;

internal sealed class AgentUsageInspectionViewModel
{
    private readonly AgentUsageCoordinator _coordinator;
    private readonly AgentUsageAvailabilityProjection _availability;

    public AgentUsageInspectionViewModel(
        AgentUsageCoordinator coordinator,
        AgentUsageAvailabilityProjection availability)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
    }

    public AgentUsageAvailabilityState Availability => _availability.CurrentState;

    public string AvailabilityCaption => Availability.FormatStatusCaption();

    public Task<AgentUsageInspectionSummary> LoadSummaryAsync() =>
        Task.FromResult(_coordinator.GetSummary(workspaceRoot: null));

    public Task<IReadOnlyList<AgentUsageRecord>> LoadRecordsAsync(
        long afterOrderingSequence,
        int maxRecords) =>
        Task.FromResult(_coordinator.GetRecords(
            workspaceRoot: null,
            afterOrderingSequence: afterOrderingSequence,
            maxRecords: maxRecords));
}
