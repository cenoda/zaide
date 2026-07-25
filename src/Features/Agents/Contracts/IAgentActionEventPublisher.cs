using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Run-scoped publisher for ordered Phase 17 action facts through the Phase 15
/// event stream owner.
/// </summary>
internal interface IAgentActionEventPublisher
{
    AgentEventId Publish(
        AgentEventKind kind,
        AgentActionFactPayload payload,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentEventId? causationEventId = null);
}
