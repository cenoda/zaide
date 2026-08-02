namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Startup load/recovery state for the durable binding document.
/// </summary>
internal enum AgentActorBackendBindingLoadState
{
    Empty,
    Loaded,
    RecoveredFromLastKnownGood,
    UnboundWithRecoveryError,
    UnsupportedSchema,
}
