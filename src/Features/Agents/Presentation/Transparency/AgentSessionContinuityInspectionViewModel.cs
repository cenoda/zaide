using System;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Presentation.Transparency;

internal sealed class AgentSessionContinuityInspectionViewModel
{
    private readonly IAgentSessionContinuityCoordinator _coordinator;
    private readonly AgentSessionContinuityAvailabilityProjection _availabilityProjection;

    public AgentSessionContinuityInspectionViewModel(
        IAgentSessionContinuityCoordinator coordinator,
        AgentSessionContinuityAvailabilityProjection availabilityProjection)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availabilityProjection = availabilityProjection
            ?? throw new ArgumentNullException(nameof(availabilityProjection));
    }

    public AgentSessionContinuityAvailabilityState Availability =>
        _availabilityProjection.CurrentState;

    public AgentSessionContinuityOperationResult Resume(AgentSessionContinuityResumeRequest request) =>
        _coordinator.Resume(request);

    public AgentSessionContinuityOperationResult Terminate(AgentSessionContinuityTerminateRequest request) =>
        _coordinator.Terminate(request);

    public AgentSessionContinuityReconcileSummary Reconcile(AgentSessionContinuityReconcileRequest request) =>
        _coordinator.Reconcile(request);
}
