namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Marker for test and future backends that may invoke the run-scoped action
/// broker. The legacy OpenAI-compatible backend does not implement this marker.
/// </summary>
internal interface IAgentActionRequestCapableBackend : IAgentBackend;
