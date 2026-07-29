using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency;

internal enum AgentTransparencyLifecycleStatus
{
    Accepted = 0,
    PartialUnavailable = 1,
    NotFound = 2,
    WorkspaceDenied = 3,
    Rejected = 4,
}
