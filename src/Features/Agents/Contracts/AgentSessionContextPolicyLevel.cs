namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Session-selectable IDE context disclosure policy levels for Phase 18.
/// Custom per-source configuration is intentionally out of scope.
/// </summary>
public enum AgentSessionContextPolicyLevel
{
    Off,
    Minimal,
    Standard,
    Detailed,
}
