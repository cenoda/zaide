using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Zaide-created backend execution context for one admitted run attempt.
/// </summary>
internal sealed record AgentBackendExecutionContext(
    AgentBackendRequest Request,
    IAgentActionBroker Actions);
