namespace Zaide.Features.Agents.Domain.Continuity;

/// <summary>
/// Separates local termination intent from process and backend acknowledgement.
/// </summary>
internal enum AgentSessionContinuityAcknowledgementState
{
    None = 0,
    LocalIntentRecorded = 1,
    LocalProcessAcknowledged = 2,
    BackendAcknowledged = 3,
    BackendAcknowledgementUnavailable = 4,
    ProviderDeletionUnverified = 5,
}
