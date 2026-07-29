using System;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Application.Transparency.Usage;

internal sealed class AgentUsageBackendEvidenceSourceWriter
{
    private readonly AgentUsageCoordinator _coordinator;

    public AgentUsageBackendEvidenceSourceWriter(
        AgentUsageCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public AgentUsageCaptureResult Submit(
        AgentUsageCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _coordinator.TrySubmit(request);
    }
}
