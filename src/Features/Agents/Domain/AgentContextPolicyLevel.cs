namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Four-level IDE context disclosure policy. Custom per-source configuration is
/// intentionally out of Phase 18 scope.
/// </summary>
internal enum AgentContextPolicyLevel
{
    Off,
    Minimal,
    Standard,
    Detailed,
}
