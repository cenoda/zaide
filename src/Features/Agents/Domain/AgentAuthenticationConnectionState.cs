namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Bounded authentication state for an actor/backend binding. Zaide never stores
/// credentials or tokens; only method selection and connection truth.
/// </summary>
internal enum AgentAuthenticationConnectionState
{
    NotRequired,
    Disconnected,
    PendingUserAction,
    Authenticated,
    Failed,
}
