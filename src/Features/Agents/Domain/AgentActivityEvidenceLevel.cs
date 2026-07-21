namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Evidence level for one normalized agent activity event.
/// </summary>
internal enum AgentActivityEvidenceLevel
{
    ZaideExecuted,
    ZaideMediated,
    BackendExecutedAndReported,
    ExternallyObserved,
    Unobservable,
}
