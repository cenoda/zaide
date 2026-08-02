namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed outcome status for durable actor/backend binding mutations.
/// </summary>
internal enum AgentActorBackendBindingMutationStatus
{
    Succeeded,
    Conflict,
    Busy,
    ValidationFailed,
    PersistenceFailed,
    RecoveryRequired,
}
