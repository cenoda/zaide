using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Presentation.Memory;

internal sealed class AgentMemoryInspectionViewModel
{
    private readonly AgentMemoryCoordinator _coordinator;
    private readonly AgentMemoryAvailabilityProjection _availability;

    public AgentMemoryInspectionViewModel(
        AgentMemoryCoordinator coordinator,
        AgentMemoryAvailabilityProjection availability)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
    }

    public AgentMemoryAvailabilityState Availability => _availability.CurrentState;

    public string AvailabilityCaption => Availability.FormatStatusCaption();

    public Task<AgentMemoryInspectionSummary> LoadSummaryAsync(string? workspaceRoot = null)
    {
        var workspaceKey = _coordinator.ResolveWorkspaceKey(workspaceRoot);
        return Task.FromResult(_coordinator.Inspector.GetSummary(workspaceKey));
    }

    public Task<IReadOnlyList<AgentMemoryRecord>> LoadRecordsAsync(
        string? workspaceRoot,
        long afterOrderingSequence,
        int maxRecords,
        bool includeDeleted = false)
    {
        var workspaceKey = _coordinator.ResolveWorkspaceKey(workspaceRoot);
        return Task.FromResult(_coordinator.Inspector.GetRecords(
            workspaceKey,
            afterOrderingSequence,
            maxRecords,
            includeDeleted));
    }

    public Task<AgentMemoryOperationResult> CreateAsync(AgentMemoryCreateRequest request) =>
        Task.FromResult(_coordinator.Create(request));

    public Task<AgentMemoryOperationResult> CorrectAsync(AgentMemoryCorrectRequest request) =>
        Task.FromResult(_coordinator.Correct(request));

    public Task<AgentMemoryOperationResult> DisableAsync(AgentMemoryDisableRequest request) =>
        Task.FromResult(_coordinator.Disable(request));

    public Task<AgentMemoryOperationResult> SupersedeAsync(AgentMemorySupersedeRequest request) =>
        Task.FromResult(_coordinator.Supersede(request));

    public Task<AgentMemoryOperationResult> DeleteAsync(AgentMemoryDeleteRequest request) =>
        Task.FromResult(_coordinator.Delete(request));
}
